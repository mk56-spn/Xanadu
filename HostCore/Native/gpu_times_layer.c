// Xanadu GPU times: a Vulkan layer that measures, on the GPU, how long every
// debug group takes. SDL_GPU has no timestamp queries and keeps its Vulkan
// handles to itself, so this sits between SDL and the driver instead:
//
//   vkBeginCommandBuffer          reset this command buffer's query pool and
//                                 open a "(command buffer)" range
//   vkCmdBeginDebugUtilsLabelEXT  open a range named after the label
//   vkCmdEndDebugUtilsLabelEXT    close the innermost range
//   vkEndCommandBuffer            close everything still open
//
// Each range is two vkCmdWriteTimestamp at BOTTOM_OF_PIPE: the time when all
// work recorded before that point has finished. So a range is the GPU time from
// the end of what came before it to the end of its own work, and siblings add up
// to their parent with nothing counted twice.
//
// Results are read without waiting, the next time any command buffer begins
// (SDL only reuses one after its fence), and go into a ring the app drains with
// xgt_read (HostCore/Rendering/GpuTimes.cs loads this same .so to call it).
//
// Enabled by the app setting VK_ADD_LAYER_PATH and VK_INSTANCE_LAYERS before
// SDL creates its instance; see GpuTimes.Install.

#include <vulkan/vk_layer.h>
#include <pthread.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#define EXPORT __attribute__((visibility("default")))

#define MAX_INSTANCES 8
#define MAX_DEVICES 8
#define MAX_SLOTS 256
#define MAX_RANGES 512
#define MAX_DEPTH 32
#define RING 16384
#define NAME 56

typedef void *Key;
static Key key_of(const void *handle) { return *(void *const *)handle; }

typedef struct
{
    Key key;
    VkInstance instance;
    PFN_vkGetInstanceProcAddr next_gipa;
    PFN_vkDestroyInstance destroy_instance;
    PFN_vkGetPhysicalDeviceProperties physical_properties;
} Instance;

typedef struct
{
    Key key;
    VkDevice device;
    double period_ns;
    uint64_t valid_mask;
    PFN_vkGetDeviceProcAddr next_gdpa;
    PFN_vkDestroyDevice destroy_device;
    PFN_vkCreateQueryPool create_query_pool;
    PFN_vkDestroyQueryPool destroy_query_pool;
    PFN_vkGetQueryPoolResults get_query_pool_results;
    PFN_vkCmdResetQueryPool cmd_reset_query_pool;
    PFN_vkCmdWriteTimestamp cmd_write_timestamp;
    PFN_vkBeginCommandBuffer begin_command_buffer;
    PFN_vkEndCommandBuffer end_command_buffer;
    PFN_vkFreeCommandBuffers free_command_buffers;
    PFN_vkCmdBeginDebugUtilsLabelEXT cmd_begin_label;
    PFN_vkCmdEndDebugUtilsLabelEXT cmd_end_label;
} Device;

typedef struct
{
    char name[NAME];
    int32_t depth;
    uint32_t begin, end;
} Range;

enum { SLOT_FREE, SLOT_RECORDING, SLOT_PENDING, SLOT_IDLE };

typedef struct
{
    VkCommandBuffer buffer;
    Device *device;
    VkQueryPool pool;
    int state;
    uint64_t serial;
    uint32_t used;
    int count;
    int stack[MAX_DEPTH];
    int top;
    Range ranges[MAX_RANGES];
} Slot;

/// What xgt_read hands out: one finished range.
typedef struct
{
    char name[NAME];
    int32_t depth;
    int32_t index;      // order within its command buffer
    uint64_t serial;    // which command buffer (in begin order)
    double start_ns;    // GPU clock
    double duration_ns;
} XgtRange;

static pthread_mutex_t lock = PTHREAD_MUTEX_INITIALIZER;
static Instance instances[MAX_INSTANCES];
static Device devices[MAX_DEVICES];
static Slot *slots[MAX_SLOTS];
static XgtRange ring[RING];
static uint64_t ring_written, ring_read, serials;

static Instance *find_instance(Key key)
{
    for (int i = 0; i < MAX_INSTANCES; i++)
        if (instances[i].key == key)
            return &instances[i];
    return NULL;
}

static Device *find_device(Key key)
{
    for (int i = 0; i < MAX_DEVICES; i++)
        if (devices[i].key == key)
            return &devices[i];
    return NULL;
}

static Slot *find_slot(VkCommandBuffer buffer, int create, Device *device)
{
    Slot *empty = NULL;
    for (int i = 0; i < MAX_SLOTS; i++)
    {
        if (slots[i] && slots[i]->buffer == buffer && slots[i]->device == device)
            return slots[i];
        if (!empty && slots[i] && slots[i]->state == SLOT_FREE && slots[i]->device == device)
            empty = slots[i];
    }
    if (!create)
        return NULL;
    if (empty)
    {
        empty->buffer = buffer;
        return empty;
    }
    for (int i = 0; i < MAX_SLOTS; i++)
    {
        if (slots[i])
            continue;
        Slot *slot = calloc(1, sizeof(Slot));
        VkQueryPoolCreateInfo info = {
            .sType = VK_STRUCTURE_TYPE_QUERY_POOL_CREATE_INFO,
            .queryType = VK_QUERY_TYPE_TIMESTAMP,
            .queryCount = MAX_RANGES * 2,
        };
        if (device->create_query_pool(device->device, &info, NULL, &slot->pool) != VK_SUCCESS)
        {
            free(slot);
            return NULL;
        }
        slot->buffer = buffer;
        slot->device = device;
        slots[i] = slot;
        return slot;
    }
    return NULL;
}

/// Copies a pending slot's ranges into the ring if the GPU has finished them; leaves it pending if not.
static void collect(Slot *slot)
{
    if (slot->state != SLOT_PENDING)
        return;
    if (slot->used == 0)
    {
        slot->state = SLOT_IDLE;
        return;
    }
    uint64_t stamps[MAX_RANGES * 2];
    Device *device = slot->device;
    VkResult result = device->get_query_pool_results(device->device, slot->pool, 0, slot->used,
        sizeof(uint64_t) * slot->used, stamps, sizeof(uint64_t), VK_QUERY_RESULT_64_BIT);
    if (result == VK_NOT_READY)
        return;
    slot->state = SLOT_IDLE;
    if (result != VK_SUCCESS)
        return;

    for (int i = 0; i < slot->count; i++)
    {
        Range *range = &slot->ranges[i];
        if (range->end == UINT32_MAX)
            continue;
        uint64_t begin = stamps[range->begin] & device->valid_mask;
        uint64_t end = stamps[range->end] & device->valid_mask;
        XgtRange *out = &ring[ring_written % RING];
        memcpy(out->name, range->name, NAME);
        out->depth = range->depth;
        out->index = i;
        out->serial = slot->serial;
        out->start_ns = (double)begin * device->period_ns;
        out->duration_ns = end >= begin ? (double)(end - begin) * device->period_ns : 0.0;
        ring_written++;
    }
    if (ring_written - ring_read > RING)
        ring_read = ring_written - RING;
}

static void open_range(Slot *slot, const char *name)
{
    if (slot->count >= MAX_RANGES || slot->top >= MAX_DEPTH || slot->used + 2 > MAX_RANGES * 2)
        return;
    Range *range = &slot->ranges[slot->count];
    strncpy(range->name, name ? name : "?", NAME - 1);
    range->name[NAME - 1] = 0;
    range->depth = slot->top;
    range->begin = slot->used++;
    range->end = UINT32_MAX;
    slot->device->cmd_write_timestamp(slot->buffer, VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, slot->pool, range->begin);
    slot->stack[slot->top++] = slot->count++;
}

static void close_range(Slot *slot)
{
    if (slot->top == 0)
        return;
    Range *range = &slot->ranges[slot->stack[--slot->top]];
    if (slot->used >= MAX_RANGES * 2)
        return;
    range->end = slot->used++;
    slot->device->cmd_write_timestamp(slot->buffer, VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, slot->pool, range->end);
}

// ---- command buffer hooks ------------------------------------------------------

static VKAPI_ATTR VkResult VKAPI_CALL BeginCommandBuffer(VkCommandBuffer buffer, const VkCommandBufferBeginInfo *info)
{
    Device *device = find_device(key_of(buffer));
    VkResult result = device->begin_command_buffer(buffer, info);
    if (result != VK_SUCCESS)
        return result;

    pthread_mutex_lock(&lock);
    for (int i = 0; i < MAX_SLOTS; i++)
        if (slots[i] && slots[i]->device == device)
            collect(slots[i]);
    Slot *slot = find_slot(buffer, 1, device);
    if (slot)
    {
        // Still not done means it was never submitted; its results are lost.
        slot->state = SLOT_RECORDING;
        slot->serial = ++serials;
        slot->used = 0;
        slot->count = 0;
        slot->top = 0;
        device->cmd_reset_query_pool(buffer, slot->pool, 0, MAX_RANGES * 2);
        open_range(slot, "(command buffer)");
    }
    pthread_mutex_unlock(&lock);
    return result;
}

static VKAPI_ATTR VkResult VKAPI_CALL EndCommandBuffer(VkCommandBuffer buffer)
{
    Device *device = find_device(key_of(buffer));
    pthread_mutex_lock(&lock);
    Slot *slot = find_slot(buffer, 0, device);
    if (slot && slot->state == SLOT_RECORDING)
    {
        while (slot->top > 0)
            close_range(slot);
        slot->state = SLOT_PENDING;
    }
    pthread_mutex_unlock(&lock);
    return device->end_command_buffer(buffer);
}

static VKAPI_ATTR void VKAPI_CALL CmdBeginDebugUtilsLabel(VkCommandBuffer buffer, const VkDebugUtilsLabelEXT *label)
{
    Device *device = find_device(key_of(buffer));
    if (device->cmd_begin_label)
        device->cmd_begin_label(buffer, label);
    pthread_mutex_lock(&lock);
    Slot *slot = find_slot(buffer, 0, device);
    if (slot && slot->state == SLOT_RECORDING)
        open_range(slot, label->pLabelName);
    pthread_mutex_unlock(&lock);
}

static VKAPI_ATTR void VKAPI_CALL CmdEndDebugUtilsLabel(VkCommandBuffer buffer)
{
    Device *device = find_device(key_of(buffer));
    pthread_mutex_lock(&lock);
    Slot *slot = find_slot(buffer, 0, device);
    // Depth 1 and up only: depth 0 is the command buffer's own range.
    if (slot && slot->state == SLOT_RECORDING && slot->top > 1)
        close_range(slot);
    pthread_mutex_unlock(&lock);
    if (device->cmd_end_label)
        device->cmd_end_label(buffer);
}

static VKAPI_ATTR void VKAPI_CALL FreeCommandBuffers(VkDevice vkDevice, VkCommandPool pool, uint32_t count, const VkCommandBuffer *buffers)
{
    Device *device = find_device(key_of(vkDevice));
    pthread_mutex_lock(&lock);
    for (uint32_t i = 0; i < count; i++)
    {
        Slot *slot = buffers[i] ? find_slot(buffers[i], 0, device) : NULL;
        if (slot)
        {
            slot->state = SLOT_FREE;
            slot->buffer = NULL;
        }
    }
    pthread_mutex_unlock(&lock);
    device->free_command_buffers(vkDevice, pool, count, buffers);
}

// ---- instance and device -------------------------------------------------------

static VKAPI_ATTR VkResult VKAPI_CALL CreateInstance(const VkInstanceCreateInfo *info, const VkAllocationCallbacks *allocator, VkInstance *out)
{
    VkLayerInstanceCreateInfo *chain = (VkLayerInstanceCreateInfo *)info->pNext;
    while (chain && !(chain->sType == VK_STRUCTURE_TYPE_LOADER_INSTANCE_CREATE_INFO && chain->function == VK_LAYER_LINK_INFO))
        chain = (VkLayerInstanceCreateInfo *)chain->pNext;
    if (!chain)
        return VK_ERROR_INITIALIZATION_FAILED;

    PFN_vkGetInstanceProcAddr next = chain->u.pLayerInfo->pfnNextGetInstanceProcAddr;
    chain->u.pLayerInfo = chain->u.pLayerInfo->pNext;
    PFN_vkCreateInstance create = (PFN_vkCreateInstance)next(NULL, "vkCreateInstance");
    VkResult result = create(info, allocator, out);
    if (result != VK_SUCCESS)
        return result;

    pthread_mutex_lock(&lock);
    Instance *slot = find_instance(NULL);
    if (slot)
    {
        slot->key = key_of(*out);
        slot->instance = *out;
        slot->next_gipa = next;
        slot->destroy_instance = (PFN_vkDestroyInstance)next(*out, "vkDestroyInstance");
        slot->physical_properties = (PFN_vkGetPhysicalDeviceProperties)next(*out, "vkGetPhysicalDeviceProperties");
    }
    pthread_mutex_unlock(&lock);
    return result;
}

static VKAPI_ATTR void VKAPI_CALL DestroyInstance(VkInstance instance, const VkAllocationCallbacks *allocator)
{
    pthread_mutex_lock(&lock);
    Instance *slot = find_instance(key_of(instance));
    PFN_vkDestroyInstance destroy = slot ? slot->destroy_instance : NULL;
    if (slot)
        memset(slot, 0, sizeof(*slot));
    pthread_mutex_unlock(&lock);
    if (destroy)
        destroy(instance, allocator);
}

static VKAPI_ATTR VkResult VKAPI_CALL CreateDevice(VkPhysicalDevice physical, const VkDeviceCreateInfo *info,
    const VkAllocationCallbacks *allocator, VkDevice *out)
{
    VkLayerDeviceCreateInfo *chain = (VkLayerDeviceCreateInfo *)info->pNext;
    while (chain && !(chain->sType == VK_STRUCTURE_TYPE_LOADER_DEVICE_CREATE_INFO && chain->function == VK_LAYER_LINK_INFO))
        chain = (VkLayerDeviceCreateInfo *)chain->pNext;
    if (!chain)
        return VK_ERROR_INITIALIZATION_FAILED;

    PFN_vkGetInstanceProcAddr next_gipa = chain->u.pLayerInfo->pfnNextGetInstanceProcAddr;
    PFN_vkGetDeviceProcAddr next_gdpa = chain->u.pLayerInfo->pfnNextGetDeviceProcAddr;
    chain->u.pLayerInfo = chain->u.pLayerInfo->pNext;
    Instance *instance = find_instance(key_of(physical));
    PFN_vkCreateDevice create = (PFN_vkCreateDevice)next_gipa(instance ? instance->instance : NULL, "vkCreateDevice");
    VkResult result = create(physical, info, allocator, out);
    if (result != VK_SUCCESS)
        return result;

    VkPhysicalDeviceProperties properties = {0};
    if (instance && instance->physical_properties)
        instance->physical_properties(physical, &properties);

    pthread_mutex_lock(&lock);
    Device *device = find_device(NULL);
    if (device)
    {
        VkDevice d = *out;
        device->key = key_of(d);
        device->device = d;
        device->period_ns = properties.limits.timestampPeriod > 0 ? properties.limits.timestampPeriod : 1.0;
        device->valid_mask = UINT64_MAX;
        device->next_gdpa = next_gdpa;
#define LOAD(field, name) device->field = (void *)next_gdpa(d, name)
        LOAD(destroy_device, "vkDestroyDevice");
        LOAD(create_query_pool, "vkCreateQueryPool");
        LOAD(destroy_query_pool, "vkDestroyQueryPool");
        LOAD(get_query_pool_results, "vkGetQueryPoolResults");
        LOAD(cmd_reset_query_pool, "vkCmdResetQueryPool");
        LOAD(cmd_write_timestamp, "vkCmdWriteTimestamp");
        LOAD(begin_command_buffer, "vkBeginCommandBuffer");
        LOAD(end_command_buffer, "vkEndCommandBuffer");
        LOAD(free_command_buffers, "vkFreeCommandBuffers");
        LOAD(cmd_begin_label, "vkCmdBeginDebugUtilsLabelEXT");
        LOAD(cmd_end_label, "vkCmdEndDebugUtilsLabelEXT");
#undef LOAD
        // Debug utils is an instance extension; some drivers only hand its commands out per instance.
        if (!device->cmd_begin_label && instance)
        {
            device->cmd_begin_label = (PFN_vkCmdBeginDebugUtilsLabelEXT)instance->next_gipa(instance->instance, "vkCmdBeginDebugUtilsLabelEXT");
            device->cmd_end_label = (PFN_vkCmdEndDebugUtilsLabelEXT)instance->next_gipa(instance->instance, "vkCmdEndDebugUtilsLabelEXT");
        }
    }
    pthread_mutex_unlock(&lock);
    return result;
}

static VKAPI_ATTR void VKAPI_CALL DestroyDevice(VkDevice vkDevice, const VkAllocationCallbacks *allocator)
{
    pthread_mutex_lock(&lock);
    Device *device = find_device(key_of(vkDevice));
    PFN_vkDestroyDevice destroy = device ? device->destroy_device : NULL;
    if (device)
    {
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (slots[i] && slots[i]->device == device)
            {
                device->destroy_query_pool(vkDevice, slots[i]->pool, NULL);
                free(slots[i]);
                slots[i] = NULL;
            }
        }
        memset(device, 0, sizeof(*device));
    }
    pthread_mutex_unlock(&lock);
    if (destroy)
        destroy(vkDevice, allocator);
}

// ---- dispatch ------------------------------------------------------------------

static PFN_vkVoidFunction device_hook(const char *name)
{
#define HOOK(fn) if (!strcmp(name, "vk" #fn)) return (PFN_vkVoidFunction)fn
    HOOK(BeginCommandBuffer);
    HOOK(EndCommandBuffer);
    HOOK(FreeCommandBuffers);
    HOOK(DestroyDevice);
    if (!strcmp(name, "vkCmdBeginDebugUtilsLabelEXT")) return (PFN_vkVoidFunction)CmdBeginDebugUtilsLabel;
    if (!strcmp(name, "vkCmdEndDebugUtilsLabelEXT")) return (PFN_vkVoidFunction)CmdEndDebugUtilsLabel;
#undef HOOK
    return NULL;
}

EXPORT VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL xgt_GetDeviceProcAddr(VkDevice device, const char *name);

EXPORT VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL xgt_GetInstanceProcAddr(VkInstance instance, const char *name)
{
    if (!strcmp(name, "vkGetInstanceProcAddr")) return (PFN_vkVoidFunction)xgt_GetInstanceProcAddr;
    if (!strcmp(name, "vkGetDeviceProcAddr")) return (PFN_vkVoidFunction)xgt_GetDeviceProcAddr;
    if (!strcmp(name, "vkCreateInstance")) return (PFN_vkVoidFunction)CreateInstance;
    if (!strcmp(name, "vkDestroyInstance")) return (PFN_vkVoidFunction)DestroyInstance;
    if (!strcmp(name, "vkCreateDevice")) return (PFN_vkVoidFunction)CreateDevice;
    PFN_vkVoidFunction hook = device_hook(name);
    if (hook)
        return hook;
    Instance *slot = instance ? find_instance(key_of(instance)) : NULL;
    return slot ? slot->next_gipa(instance, name) : NULL;
}

EXPORT VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL xgt_GetDeviceProcAddr(VkDevice vkDevice, const char *name)
{
    if (!strcmp(name, "vkGetDeviceProcAddr")) return (PFN_vkVoidFunction)xgt_GetDeviceProcAddr;
    Device *device = find_device(key_of(vkDevice));
    if (!device)
        return NULL;
    PFN_vkVoidFunction hook = device_hook(name);
    // Only hand out a hook when the driver has the call underneath it.
    if (hook && device->next_gdpa(vkDevice, name))
        return hook;
    if (hook && (!strcmp(name, "vkCmdBeginDebugUtilsLabelEXT") || !strcmp(name, "vkCmdEndDebugUtilsLabelEXT")) && device->cmd_begin_label)
        return hook;
    return device->next_gdpa(vkDevice, name);
}

// ---- for the app ---------------------------------------------------------------

/// Copies up to max finished ranges, oldest first, and forgets them. Returns how many.
EXPORT int xgt_read(XgtRange *out, int max)
{
    pthread_mutex_lock(&lock);
    int n = 0;
    while (ring_read < ring_written && n < max)
        out[n++] = ring[ring_read++ % RING];
    pthread_mutex_unlock(&lock);
    return n;
}

/// Whether a device is being timed (the layer is in the chain and saw vkCreateDevice).
EXPORT int xgt_active(void)
{
    pthread_mutex_lock(&lock);
    int active = 0;
    for (int i = 0; i < MAX_DEVICES; i++)
        active |= devices[i].key != NULL;
    pthread_mutex_unlock(&lock);
    return active;
}
