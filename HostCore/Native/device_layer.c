// Xanadu device layer: a Vulkan layer under SDL_GPU that gives the app the Vulkan SDL
// leaves out. SDL_GPU only exposes what every backend has; we are Vulkan only, so this
// sits between SDL and the driver and changes what SDL asks for:
//
//   vkCreateInstance   raises the API version to what the loader has, up to 1.4
//   vkCreateDevice     switches on every feature the device has (bar robustness and
//                      sparse memory) and the extensions in `extras`, with all their features
//   vkCreateBuffer     storage buffers get a GPU address (buffer device address)
//   vkAllocateMemory   memory is allocated so buffers in it can have one
//   vkCmdDispatch …    a memory barrier first, once the app holds any address: SDL
//                      can't see what a shader reaches through a pointer, so it can't
//                      order those writes; this does (see `sync`)
//   vkCmdInsertDebugUtilsLabelEXT
//                      labels named "xdv:…" are commands to this layer and never reach
//                      the driver: "xdv:barrier", "xdv:sync on", "xdv:sync off"
//   mesh shaders       SDL_GPU has none, so a "vertex" shader whose SPIR-V is a mesh entry
//                      point becomes one: its pipeline's stage is MESH (no vertex input),
//                      descriptor layouts open the vertex stage's bindings to mesh and task,
//                      barriers naming the vertex stage name them too, and vkCmdDraw /
//                      vkCmdDrawIndirect with a mesh pipeline bound draw mesh tasks
//                      (x = vertices, y = instances, z = 1; indirect reads x, y, z from the
//                      args as they are, so a mesh draw's first vertex is 1)
//
// The app reaches it through the same .so (Rendering/DeviceLayer.cs): xdv_address turns
// an SDL_GPUBuffer into its GPU address, xdv_report says what was switched on.
//
// xdv_address reads SDL's own structs (SDL 3.2's VulkanBufferContainer and VulkanBuffer)
// and checks what it read against the buffers this layer saw made, so a different SDL
// returns 0 rather than a wrong address.
//
// Enabled by the app setting VK_ADD_LAYER_PATH and VK_INSTANCE_LAYERS before SDL creates
// its instance; see DeviceLayer.Install.

#include <vulkan/vk_layer.h>
#include <pthread.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define EXPORT __attribute__((visibility("default")))
#define MAX_API VK_API_VERSION_1_4
#define BUFFERS (1 << 16)
#define REPORT 4096

typedef void *Key;
static Key key_of(const void *handle) { return *(void *const *)handle; }

static pthread_mutex_t lock = PTHREAD_MUTEX_INITIALIZER;

// ---- instance ------------------------------------------------------------------

static struct
{
    Key key;
    VkInstance instance;
    uint32_t api;
    PFN_vkGetInstanceProcAddr next_gipa;
    PFN_vkDestroyInstance destroy_instance;
    PFN_vkGetPhysicalDeviceProperties physical_properties;
    PFN_vkGetPhysicalDeviceFeatures2 physical_features;
    PFN_vkEnumerateDeviceExtensionProperties device_extensions;
} instance;

// ---- device --------------------------------------------------------------------

static struct
{
    Key key;
    VkDevice device;
    int addresses;   // bufferDeviceAddress is on
    int handed_out;  // the app holds an address: barriers go in
    int sync;        // "xdv:sync off" turns the automatic barriers off
    PFN_vkGetDeviceProcAddr next_gdpa;
    PFN_vkDestroyDevice destroy_device;
    PFN_vkCreateBuffer create_buffer;
    PFN_vkDestroyBuffer destroy_buffer;
    PFN_vkAllocateMemory allocate_memory;
    PFN_vkGetBufferDeviceAddress buffer_address;
    PFN_vkCmdPipelineBarrier cmd_barrier;
    PFN_vkCmdDispatch cmd_dispatch;
    PFN_vkCmdDispatchIndirect cmd_dispatch_indirect;
    PFN_vkCmdBeginRenderPass cmd_begin_render_pass;
    PFN_vkCmdCopyBuffer cmd_copy_buffer;
    PFN_vkCmdCopyBufferToImage cmd_copy_buffer_to_image;
    PFN_vkCmdInsertDebugUtilsLabelEXT cmd_insert_label;
    int mesh;        // mesh shaders are on
    PFN_vkCreateShaderModule create_shader_module;
    PFN_vkDestroyShaderModule destroy_shader_module;
    PFN_vkCreateGraphicsPipelines create_graphics_pipelines;
    PFN_vkDestroyPipeline destroy_pipeline;
    PFN_vkCreateDescriptorSetLayout create_set_layout;
    PFN_vkCmdBindPipeline cmd_bind_pipeline;
    PFN_vkCmdDraw cmd_draw;
    PFN_vkCmdDrawIndirect cmd_draw_indirect;
    PFN_vkCmdDrawMeshTasksEXT cmd_draw_mesh;
    PFN_vkCmdDrawMeshTasksIndirectEXT cmd_draw_mesh_indirect;
} device;

static char report[REPORT];

static void say(const char *format, const char *text)
{
    size_t used = strlen(report);
    if (used + 1 < REPORT)
        snprintf(report + used, REPORT - used, format, text);
}

// ---- buffers: every storage buffer SDL made, and its address once asked ---------

static struct { uint64_t buffer, address; } buffers[BUFFERS];
static const uint64_t GONE = 1;

static uint64_t *find_buffer(uint64_t buffer, int insert)
{
    uint32_t at = (uint32_t)((buffer * 0x9E3779B97F4A7C15ull) >> 48) & (BUFFERS - 1);
    uint64_t *gone = NULL;
    for (int i = 0; i < BUFFERS; i++, at = (at + 1) & (BUFFERS - 1))
    {
        if (buffers[at].buffer == buffer)
            return &buffers[at].address;
        if (buffers[at].buffer == GONE && !gone)
            gone = &buffers[at].address;
        if (buffers[at].buffer == 0)
        {
            if (!insert)
                return NULL;
            uint64_t *slot = gone ? gone : &buffers[at].address;
            slot[-1] = buffer;
            *slot = 0;
            return slot;
        }
    }
    if (insert && gone)
    {
        gone[-1] = buffer;
        *gone = 0;
        return gone;
    }
    return NULL;
}

static VKAPI_ATTR VkResult VKAPI_CALL CreateBuffer(VkDevice d, const VkBufferCreateInfo *info, const VkAllocationCallbacks *allocator, VkBuffer *out)
{
    VkBufferCreateInfo copy = *info;
    int storage = device.addresses && (info->usage & VK_BUFFER_USAGE_STORAGE_BUFFER_BIT);
    if (storage)
        copy.usage |= VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
    VkResult result = device.create_buffer(d, &copy, allocator, out);
    if (result == VK_SUCCESS && storage)
    {
        pthread_mutex_lock(&lock);
        find_buffer((uint64_t)*out, 1);
        pthread_mutex_unlock(&lock);
    }
    return result;
}

static VKAPI_ATTR void VKAPI_CALL DestroyBuffer(VkDevice d, VkBuffer buffer, const VkAllocationCallbacks *allocator)
{
    pthread_mutex_lock(&lock);
    uint64_t *slot = buffer ? find_buffer((uint64_t)buffer, 0) : NULL;
    if (slot)
    {
        slot[-1] = GONE;
        *slot = 0;
    }
    pthread_mutex_unlock(&lock);
    device.destroy_buffer(d, buffer, allocator);
}

static VKAPI_ATTR VkResult VKAPI_CALL AllocateMemory(VkDevice d, const VkMemoryAllocateInfo *info, const VkAllocationCallbacks *allocator, VkDeviceMemory *out)
{
    if (!device.addresses)
        return device.allocate_memory(d, info, allocator, out);
    for (const VkBaseInStructure *s = info->pNext; s; s = s->pNext)
        if (s->sType == VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_FLAGS_INFO)
            return device.allocate_memory(d, info, allocator, out);
    VkMemoryAllocateFlagsInfo flags = {
        .sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_FLAGS_INFO,
        .pNext = info->pNext,
        .flags = VK_MEMORY_ALLOCATE_DEVICE_ADDRESS_BIT,
    };
    VkMemoryAllocateInfo copy = *info;
    copy.pNext = &flags;
    return device.allocate_memory(d, &copy, allocator, out);
}

// ---- sync: what SDL can't order -------------------------------------------------

static void barrier(VkCommandBuffer buffer)
{
    VkMemoryBarrier memory = {
        .sType = VK_STRUCTURE_TYPE_MEMORY_BARRIER,
        .srcAccessMask = VK_ACCESS_SHADER_WRITE_BIT | VK_ACCESS_TRANSFER_WRITE_BIT,
        .dstAccessMask = VK_ACCESS_SHADER_READ_BIT | VK_ACCESS_SHADER_WRITE_BIT | VK_ACCESS_INDIRECT_COMMAND_READ_BIT
            | VK_ACCESS_TRANSFER_READ_BIT | VK_ACCESS_TRANSFER_WRITE_BIT,
    };
    VkPipelineStageFlags shaders = VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT | VK_PIPELINE_STAGE_VERTEX_SHADER_BIT
        | VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT
        | (device.mesh ? VK_PIPELINE_STAGE_MESH_SHADER_BIT_EXT | VK_PIPELINE_STAGE_TASK_SHADER_BIT_EXT : 0);
    device.cmd_barrier(buffer, shaders | VK_PIPELINE_STAGE_TRANSFER_BIT,
        shaders | VK_PIPELINE_STAGE_DRAW_INDIRECT_BIT | VK_PIPELINE_STAGE_TRANSFER_BIT, 0, 1, &memory, 0, NULL, 0, NULL);
}

static void sync(VkCommandBuffer buffer)
{
    if (device.handed_out && device.sync)
        barrier(buffer);
}

static VKAPI_ATTR void VKAPI_CALL CmdDispatch(VkCommandBuffer b, uint32_t x, uint32_t y, uint32_t z)
{
    sync(b);
    device.cmd_dispatch(b, x, y, z);
}

static VKAPI_ATTR void VKAPI_CALL CmdDispatchIndirect(VkCommandBuffer b, VkBuffer args, VkDeviceSize offset)
{
    sync(b);
    device.cmd_dispatch_indirect(b, args, offset);
}

static VKAPI_ATTR void VKAPI_CALL CmdBeginRenderPass(VkCommandBuffer b, const VkRenderPassBeginInfo *info, VkSubpassContents contents)
{
    sync(b);
    device.cmd_begin_render_pass(b, info, contents);
}

static VKAPI_ATTR void VKAPI_CALL CmdCopyBuffer(VkCommandBuffer b, VkBuffer from, VkBuffer to, uint32_t count, const VkBufferCopy *regions)
{
    sync(b);
    device.cmd_copy_buffer(b, from, to, count, regions);
}

static VKAPI_ATTR void VKAPI_CALL CmdCopyBufferToImage(VkCommandBuffer b, VkBuffer from, VkImage to, VkImageLayout layout, uint32_t count, const VkBufferImageCopy *regions)
{
    sync(b);
    device.cmd_copy_buffer_to_image(b, from, to, layout, count, regions);
}

static VKAPI_ATTR void VKAPI_CALL CmdInsertDebugUtilsLabel(VkCommandBuffer b, const VkDebugUtilsLabelEXT *label)
{
    const char *name = label && label->pLabelName ? label->pLabelName : "";
    if (strncmp(name, "xdv:", 4) != 0)
    {
        if (device.cmd_insert_label)
            device.cmd_insert_label(b, label);
        return;
    }
    name += 4;
    if (!strcmp(name, "barrier"))
        barrier(b);
    else if (!strcmp(name, "sync on"))
        device.sync = 1;
    else if (!strcmp(name, "sync off"))
        device.sync = 0;
}

// ---- mesh shaders: SDL's vertex stage, made a mesh stage ------------------------

#define HANDLES 4096

/// A set of handles (shader modules, pipelines) or a map of command buffer → handle, open addressed.
typedef struct { uint64_t key, value; } Slot;
static Slot mesh_modules[HANDLES], mesh_pipelines[HANDLES], bound[HANDLES];

static Slot *slot_of(Slot *table, uint64_t key, int insert)
{
    uint32_t at = (uint32_t)((key * 0x9E3779B97F4A7C15ull) >> 52) & (HANDLES - 1);
    Slot *gone = NULL;
    for (int i = 0; i < HANDLES; i++, at = (at + 1) & (HANDLES - 1))
    {
        if (table[at].key == key)
            return &table[at];
        if (table[at].key == GONE && !gone)
            gone = &table[at];
        if (table[at].key == 0)
        {
            if (!insert)
                return NULL;
            Slot *slot = gone ? gone : &table[at];
            slot->key = key;
            slot->value = 0;
            return slot;
        }
    }
    if (insert && gone)
    {
        gone->key = key;
        gone->value = 0;
    }
    return insert ? gone : NULL;
}

static int has(Slot *table, uint64_t key)
{
    pthread_mutex_lock(&lock);
    int found = key && slot_of(table, key, 0) != NULL;
    pthread_mutex_unlock(&lock);
    return found;
}

static void put(Slot *table, uint64_t key, uint64_t value)
{
    pthread_mutex_lock(&lock);
    Slot *slot = slot_of(table, key, 1);
    if (slot)
        slot->value = value;
    pthread_mutex_unlock(&lock);
}

static void drop(Slot *table, uint64_t key)
{
    pthread_mutex_lock(&lock);
    Slot *slot = slot_of(table, key, 0);
    if (slot)
        slot->key = GONE;
    pthread_mutex_unlock(&lock);
}

/// Whether SPIR-V declares a mesh entry point (OpEntryPoint, execution model MeshEXT).
static int is_mesh(const uint32_t *code, size_t bytes)
{
    size_t words = bytes / 4;
    for (size_t at = 5; at < words;)
    {
        uint32_t count = code[at] >> 16, op = code[at] & 0xFFFF;
        if (count == 0)
            return 0;
        if (op == 15 && at + 1 < words && code[at + 1] == 5365)
            return 1;
        if (op == 54) // OpFunction: entry points are all declared before
            return 0;
        at += count;
    }
    return 0;
}

static VKAPI_ATTR VkResult VKAPI_CALL CreateShaderModule(VkDevice d, const VkShaderModuleCreateInfo *info,
    const VkAllocationCallbacks *allocator, VkShaderModule *out)
{
    VkResult result = device.create_shader_module(d, info, allocator, out);
    if (result == VK_SUCCESS && device.mesh && is_mesh(info->pCode, info->codeSize))
        put(mesh_modules, (uint64_t)*out, 1);
    return result;
}

static VKAPI_ATTR void VKAPI_CALL DestroyShaderModule(VkDevice d, VkShaderModule module, const VkAllocationCallbacks *allocator)
{
    drop(mesh_modules, (uint64_t)module);
    device.destroy_shader_module(d, module, allocator);
}

static VKAPI_ATTR VkResult VKAPI_CALL CreateGraphicsPipelines(VkDevice d, VkPipelineCache cache, uint32_t count,
    const VkGraphicsPipelineCreateInfo *infos, const VkAllocationCallbacks *allocator, VkPipeline *out)
{
    VkGraphicsPipelineCreateInfo *copies = NULL;
    VkPipelineShaderStageCreateInfo *stages = NULL;
    int *meshed = calloc(count ? count : 1, sizeof(int));
    uint32_t total = 0;
    for (uint32_t i = 0; i < count; i++)
        for (uint32_t k = 0; k < infos[i].stageCount; k++)
            if (has(mesh_modules, (uint64_t)infos[i].pStages[k].module))
                meshed[i] = 1;
    for (uint32_t i = 0; i < count; i++)
        total += meshed[i] ? infos[i].stageCount : 0;
    if (total)
    {
        copies = malloc(count * sizeof(*infos));
        stages = malloc(total * sizeof(*stages));
        memcpy(copies, infos, count * sizeof(*infos));
        uint32_t at = 0;
        for (uint32_t i = 0; i < count; i++)
        {
            if (!meshed[i])
                continue;
            memcpy(stages + at, infos[i].pStages, infos[i].stageCount * sizeof(*stages));
            for (uint32_t k = 0; k < infos[i].stageCount; k++)
                if (stages[at + k].stage == VK_SHADER_STAGE_VERTEX_BIT)
                    stages[at + k].stage = VK_SHADER_STAGE_MESH_BIT_EXT;
            copies[i].pStages = stages + at;
            copies[i].pVertexInputState = NULL;
            copies[i].pInputAssemblyState = NULL;
            at += infos[i].stageCount;
        }
    }
    VkResult result = device.create_graphics_pipelines(d, cache, count, copies ? copies : infos, allocator, out);
    if (result == VK_SUCCESS)
        for (uint32_t i = 0; i < count; i++)
            if (meshed[i])
                put(mesh_pipelines, (uint64_t)out[i], 1);
    free(copies);
    free(stages);
    free(meshed);
    return result;
}

static VKAPI_ATTR void VKAPI_CALL DestroyPipeline(VkDevice d, VkPipeline pipeline, const VkAllocationCallbacks *allocator)
{
    drop(mesh_pipelines, (uint64_t)pipeline);
    device.destroy_pipeline(d, pipeline, allocator);
}

static VKAPI_ATTR VkResult VKAPI_CALL CreateDescriptorSetLayout(VkDevice d, const VkDescriptorSetLayoutCreateInfo *info,
    const VkAllocationCallbacks *allocator, VkDescriptorSetLayout *out)
{
    if (!device.mesh || info->bindingCount == 0)
        return device.create_set_layout(d, info, allocator, out);
    VkDescriptorSetLayoutBinding *bindings = malloc(info->bindingCount * sizeof(*bindings));
    memcpy(bindings, info->pBindings, info->bindingCount * sizeof(*bindings));
    for (uint32_t i = 0; i < info->bindingCount; i++)
        if (bindings[i].stageFlags & VK_SHADER_STAGE_VERTEX_BIT)
            bindings[i].stageFlags |= VK_SHADER_STAGE_MESH_BIT_EXT | VK_SHADER_STAGE_TASK_BIT_EXT;
    VkDescriptorSetLayoutCreateInfo copy = *info;
    copy.pBindings = bindings;
    VkResult result = device.create_set_layout(d, &copy, allocator, out);
    free(bindings);
    return result;
}

static VKAPI_ATTR void VKAPI_CALL CmdBindPipeline(VkCommandBuffer b, VkPipelineBindPoint point, VkPipeline pipeline)
{
    if (device.mesh && point == VK_PIPELINE_BIND_POINT_GRAPHICS)
        put(bound, (uint64_t)b, has(mesh_pipelines, (uint64_t)pipeline));
    device.cmd_bind_pipeline(b, point, pipeline);
}

static int mesh_bound(VkCommandBuffer b)
{
    if (!device.mesh)
        return 0;
    pthread_mutex_lock(&lock);
    Slot *slot = slot_of(bound, (uint64_t)b, 0);
    int mesh = slot && slot->value;
    pthread_mutex_unlock(&lock);
    return mesh;
}

static VKAPI_ATTR void VKAPI_CALL CmdDraw(VkCommandBuffer b, uint32_t vertices, uint32_t instances, uint32_t first, uint32_t firstInstance)
{
    if (mesh_bound(b))
        device.cmd_draw_mesh(b, vertices, instances, 1);
    else
        device.cmd_draw(b, vertices, instances, first, firstInstance);
}

static VKAPI_ATTR void VKAPI_CALL CmdDrawIndirect(VkCommandBuffer b, VkBuffer args, VkDeviceSize offset, uint32_t count, uint32_t stride)
{
    if (mesh_bound(b))
        device.cmd_draw_mesh_indirect(b, args, offset, count, stride);
    else
        device.cmd_draw_indirect(b, args, offset, count, stride);
}

static VkPipelineStageFlags with_mesh(VkPipelineStageFlags stages)
{
    return device.mesh && (stages & VK_PIPELINE_STAGE_VERTEX_SHADER_BIT)
        ? stages | VK_PIPELINE_STAGE_MESH_SHADER_BIT_EXT | VK_PIPELINE_STAGE_TASK_SHADER_BIT_EXT : stages;
}

static VKAPI_ATTR void VKAPI_CALL CmdPipelineBarrier(VkCommandBuffer b, VkPipelineStageFlags from, VkPipelineStageFlags to,
    VkDependencyFlags flags, uint32_t memories, const VkMemoryBarrier *memory, uint32_t buffers, const VkBufferMemoryBarrier *buffer,
    uint32_t images, const VkImageMemoryBarrier *image)
{
    device.cmd_barrier(b, with_mesh(from), with_mesh(to), flags, memories, memory, buffers, buffer, images, image);
}

// ---- device creation: features and extensions ----------------------------------

/// An extension switched on when the device has it, with every feature its struct reports.
typedef struct
{
    const char *name;
    VkStructureType type;
    size_t size;
    uint32_t api; // the device version it needs
} Extra;

static const Extra extras[] = {
    { VK_EXT_SHADER_ATOMIC_FLOAT_EXTENSION_NAME, VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_SHADER_ATOMIC_FLOAT_FEATURES_EXT,
        sizeof(VkPhysicalDeviceShaderAtomicFloatFeaturesEXT), VK_API_VERSION_1_0 },
    { VK_EXT_SHADER_ATOMIC_FLOAT_2_EXTENSION_NAME, VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_SHADER_ATOMIC_FLOAT_2_FEATURES_EXT,
        sizeof(VkPhysicalDeviceShaderAtomicFloat2FeaturesEXT), VK_API_VERSION_1_0 },
    { VK_KHR_SHADER_CLOCK_EXTENSION_NAME, VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_SHADER_CLOCK_FEATURES_KHR,
        sizeof(VkPhysicalDeviceShaderClockFeaturesKHR), VK_API_VERSION_1_0 },
    { VK_EXT_SHADER_IMAGE_ATOMIC_INT64_EXTENSION_NAME, VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_SHADER_IMAGE_ATOMIC_INT64_FEATURES_EXT,
        sizeof(VkPhysicalDeviceShaderImageAtomicInt64FeaturesEXT), VK_API_VERSION_1_0 },
    { VK_KHR_COMPUTE_SHADER_DERIVATIVES_EXTENSION_NAME, VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_COMPUTE_SHADER_DERIVATIVES_FEATURES_KHR,
        sizeof(VkPhysicalDeviceComputeShaderDerivativesFeaturesKHR), VK_API_VERSION_1_0 },
    { VK_EXT_MESH_SHADER_EXTENSION_NAME, VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_MESH_SHADER_FEATURES_EXT,
        sizeof(VkPhysicalDeviceMeshShaderFeaturesEXT), VK_API_VERSION_1_2 },
    { VK_EXT_DEVICE_GENERATED_COMMANDS_EXTENSION_NAME, VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DEVICE_GENERATED_COMMANDS_FEATURES_EXT,
        sizeof(VkPhysicalDeviceDeviceGeneratedCommandsFeaturesEXT), VK_API_VERSION_1_4 },
};
#define EXTRAS (int)(sizeof(extras) / sizeof(extras[0]))

/// Everything the device is asked for: the core structs, then one per extra it has.
typedef struct
{
    VkPhysicalDeviceFeatures2 core;
    VkPhysicalDeviceVulkan11Features v11;
    VkPhysicalDeviceVulkan12Features v12;
    VkPhysicalDeviceVulkan13Features v13;
    VkPhysicalDeviceVulkan14Features v14;
    VkBaseOutStructure *extra[EXTRAS];
    const char *names[256];
} Ask;

static int in_chain(const void *chain, VkStructureType type)
{
    for (const VkBaseInStructure *s = chain; s; s = s->pNext)
        if (s->sType == type)
            return 1;
    return 0;
}

static void link(void ***tail, void *s)
{
    **tail = s;
    *tail = (void **)&((VkBaseOutStructure *)s)->pNext;
}

/// Fills `ask` with what the device has and chains it; the same structs are then what's switched on.
static void query(Ask *ask, VkPhysicalDevice physical, uint32_t api, const int *have)
{
    memset(ask, 0, sizeof(*ask) - sizeof(ask->names));
    ask->core.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2;
    ask->v11.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_1_FEATURES;
    ask->v12.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES;
    ask->v13.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES;
    ask->v14.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_4_FEATURES;
    void **tail = &ask->core.pNext;
    if (api >= VK_API_VERSION_1_2)
    {
        link(&tail, &ask->v11);
        link(&tail, &ask->v12);
    }
    if (api >= VK_API_VERSION_1_3)
        link(&tail, &ask->v13);
    if (api >= VK_API_VERSION_1_4)
        link(&tail, &ask->v14);
    for (int i = 0; i < EXTRAS; i++)
    {
        if (!have[i])
            continue;
        ask->extra[i] = calloc(1, extras[i].size);
        ask->extra[i]->sType = extras[i].type;
        link(&tail, ask->extra[i]);
    }
    instance.physical_features(physical, &ask->core);
}

/// What stays off though the device has it: robustness costs on every access, sparse memory changes allocation.
static void trim(Ask *ask)
{
    VkPhysicalDeviceFeatures *f = &ask->core.features;
    f->robustBufferAccess = VK_FALSE;
    f->sparseBinding = f->sparseResidencyBuffer = f->sparseResidencyImage2D = f->sparseResidencyImage3D = VK_FALSE;
    f->sparseResidency2Samples = f->sparseResidency4Samples = f->sparseResidency8Samples = VK_FALSE;
    f->sparseResidency16Samples = f->sparseResidencyAliased = VK_FALSE;
    ask->v12.bufferDeviceAddressCaptureReplay = ask->v12.bufferDeviceAddressMultiDevice = VK_FALSE;
    ask->v13.robustImageAccess = VK_FALSE;
    ask->v14.pipelineRobustness = VK_FALSE;
    for (int i = 0; i < EXTRAS; i++)
        if (ask->extra[i] && ask->extra[i]->sType == VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_MESH_SHADER_FEATURES_EXT)
            ((VkPhysicalDeviceMeshShaderFeaturesEXT *)ask->extra[i])->primitiveFragmentShadingRateMeshShader = VK_FALSE;
}

/// Leaves out of the chain every struct the app already gave (it keeps its own), and ORs the app's core features in.
static void merge(Ask *ask, const VkDeviceCreateInfo *info)
{
    if (info->pEnabledFeatures)
    {
        const VkBool32 *theirs = (const VkBool32 *)info->pEnabledFeatures;
        VkBool32 *ours = (VkBool32 *)&ask->core.features;
        for (size_t i = 0; i < sizeof(VkPhysicalDeviceFeatures) / sizeof(VkBool32); i++)
            ours[i] |= theirs[i];
    }
    void **tail = &ask->core.pNext;
    for (VkBaseOutStructure *s = *tail; s;)
    {
        VkBaseOutStructure *next = s->pNext;
        if (!in_chain(info->pNext, s->sType))
            link(&tail, s);
        s = next;
    }
    *tail = (void *)info->pNext;
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
    PFN_vkCreateDevice create = (PFN_vkCreateDevice)next_gipa(instance.instance, "vkCreateDevice");

    VkPhysicalDeviceProperties properties = {0};
    instance.physical_properties(physical, &properties);
    uint32_t api = properties.apiVersion < instance.api ? properties.apiVersion : instance.api;

    uint32_t count = 0;
    instance.device_extensions(physical, NULL, &count, NULL);
    VkExtensionProperties *listed = calloc(count ? count : 1, sizeof(VkExtensionProperties));
    instance.device_extensions(physical, NULL, &count, listed);
    int have[EXTRAS] = {0};
    for (int i = 0; i < EXTRAS; i++)
        for (uint32_t j = 0; j < count && extras[i].api <= api; j++)
            have[i] |= !strcmp(listed[j].extensionName, extras[i].name);
    free(listed);

    Ask *ask = calloc(1, sizeof(Ask));
    VkDeviceCreateInfo copy = *info;
    int names = 0;
    for (uint32_t i = 0; i < info->enabledExtensionCount && names < 200; i++)
        ask->names[names++] = info->ppEnabledExtensionNames[i];
    VkResult result;
    if (api >= VK_API_VERSION_1_2 && instance.physical_features && !in_chain(info->pNext, VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2))
    {
        query(ask, physical, api, have);
        trim(ask);
        merge(ask, info);
        for (int i = 0; i < EXTRAS; i++)
        {
            int listed_already = 0;
            for (int j = 0; j < names; j++)
                listed_already |= !strcmp(ask->names[j], extras[i].name);
            if (have[i] && !listed_already)
                ask->names[names++] = extras[i].name;
        }
        copy.pNext = &ask->core;
        copy.pEnabledFeatures = NULL;
        copy.enabledExtensionCount = (uint32_t)names;
        copy.ppEnabledExtensionNames = ask->names;
        result = create(physical, &copy, allocator, out);
    }
    else
    {
        // An app that brings its own Features2, or a device older than 1.2: leave it as asked.
        result = create(physical, info, allocator, out);
        memset(ask, 0, sizeof(*ask));
    }

    if (result == VK_SUCCESS)
    {
        pthread_mutex_lock(&lock);
        VkDevice d = *out;
        memset(&device, 0, sizeof(device));
        device.key = key_of(d);
        device.device = d;
        device.sync = 1;
        device.addresses = ask->v12.bufferDeviceAddress == VK_TRUE;
        device.next_gdpa = next_gdpa;
#define LOAD(field, name) device.field = (void *)next_gdpa(d, name)
        LOAD(destroy_device, "vkDestroyDevice");
        LOAD(create_buffer, "vkCreateBuffer");
        LOAD(destroy_buffer, "vkDestroyBuffer");
        LOAD(allocate_memory, "vkAllocateMemory");
        LOAD(buffer_address, "vkGetBufferDeviceAddress");
        LOAD(cmd_barrier, "vkCmdPipelineBarrier");
        LOAD(cmd_dispatch, "vkCmdDispatch");
        LOAD(cmd_dispatch_indirect, "vkCmdDispatchIndirect");
        LOAD(cmd_begin_render_pass, "vkCmdBeginRenderPass");
        LOAD(cmd_copy_buffer, "vkCmdCopyBuffer");
        LOAD(cmd_copy_buffer_to_image, "vkCmdCopyBufferToImage");
        LOAD(cmd_insert_label, "vkCmdInsertDebugUtilsLabelEXT");
        LOAD(create_shader_module, "vkCreateShaderModule");
        LOAD(destroy_shader_module, "vkDestroyShaderModule");
        LOAD(create_graphics_pipelines, "vkCreateGraphicsPipelines");
        LOAD(destroy_pipeline, "vkDestroyPipeline");
        LOAD(create_set_layout, "vkCreateDescriptorSetLayout");
        LOAD(cmd_bind_pipeline, "vkCmdBindPipeline");
        LOAD(cmd_draw, "vkCmdDraw");
        LOAD(cmd_draw_indirect, "vkCmdDrawIndirect");
        LOAD(cmd_draw_mesh, "vkCmdDrawMeshTasksEXT");
        LOAD(cmd_draw_mesh_indirect, "vkCmdDrawMeshTasksIndirectEXT");
#undef LOAD
        for (int i = 0; i < EXTRAS; i++)
            if (!strcmp(extras[i].name, VK_EXT_MESH_SHADER_EXTENSION_NAME) && have[i] && ask->extra[i])
                device.mesh = ((VkPhysicalDeviceMeshShaderFeaturesEXT *)ask->extra[i])->meshShader == VK_TRUE
                    && device.cmd_draw_mesh && device.cmd_draw_mesh_indirect;
        if (!device.cmd_insert_label)
            device.cmd_insert_label = (void *)instance.next_gipa(instance.instance, "vkCmdInsertDebugUtilsLabelEXT");

        report[0] = 0;
        char line[320];
        snprintf(line, sizeof(line), "api %u.%u, %s", VK_API_VERSION_MAJOR(api), VK_API_VERSION_MINOR(api), properties.deviceName);
        say("%s\n", line);
        say("%s\n", device.addresses ? "buffer addresses on" : "buffer addresses OFF");
        say("%s\n", ask->core.features.shaderInt64 ? "int64 on" : "int64 OFF");
        say("%s\n", ask->v12.shaderBufferInt64Atomics ? "int64 buffer atomics on" : "int64 buffer atomics OFF");
        for (int i = 0; i < EXTRAS; i++)
            say(have[i] && ask->extra[i] ? "%s on\n" : "%s OFF\n", extras[i].name);
        say("%s\n", device.mesh ? "mesh draws on" : "mesh draws OFF");
        pthread_mutex_unlock(&lock);
    }
    for (int i = 0; i < EXTRAS; i++)
        free(ask->extra[i]);
    free(ask);
    return result;
}

static VKAPI_ATTR void VKAPI_CALL DestroyDevice(VkDevice d, const VkAllocationCallbacks *allocator)
{
    pthread_mutex_lock(&lock);
    PFN_vkDestroyDevice destroy = device.key == key_of(d) ? device.destroy_device : NULL;
    if (destroy)
    {
        memset(&device, 0, sizeof(device));
        memset(buffers, 0, sizeof(buffers));
        memset(mesh_modules, 0, sizeof(mesh_modules));
        memset(mesh_pipelines, 0, sizeof(mesh_pipelines));
        memset(bound, 0, sizeof(bound));
    }
    pthread_mutex_unlock(&lock);
    if (destroy)
        destroy(d, allocator);
}

// ---- instance creation: the API version ---------------------------------------

static VKAPI_ATTR VkResult VKAPI_CALL CreateInstance(const VkInstanceCreateInfo *info, const VkAllocationCallbacks *allocator, VkInstance *out)
{
    VkLayerInstanceCreateInfo *chain = (VkLayerInstanceCreateInfo *)info->pNext;
    while (chain && !(chain->sType == VK_STRUCTURE_TYPE_LOADER_INSTANCE_CREATE_INFO && chain->function == VK_LAYER_LINK_INFO))
        chain = (VkLayerInstanceCreateInfo *)chain->pNext;
    if (!chain)
        return VK_ERROR_INITIALIZATION_FAILED;
    PFN_vkGetInstanceProcAddr next = chain->u.pLayerInfo->pfnNextGetInstanceProcAddr;
    chain->u.pLayerInfo = chain->u.pLayerInfo->pNext;

    uint32_t loader = VK_API_VERSION_1_0;
    PFN_vkEnumerateInstanceVersion version = (PFN_vkEnumerateInstanceVersion)next(NULL, "vkEnumerateInstanceVersion");
    if (version)
        version(&loader);
    VkApplicationInfo app = { .sType = VK_STRUCTURE_TYPE_APPLICATION_INFO, .apiVersion = VK_API_VERSION_1_0 };
    if (info->pApplicationInfo)
        app = *info->pApplicationInfo;
    uint32_t want = loader < MAX_API ? loader : MAX_API;
    if (app.apiVersion < want)
        app.apiVersion = want;
    VkInstanceCreateInfo copy = *info;
    copy.pApplicationInfo = &app;

    PFN_vkCreateInstance create = (PFN_vkCreateInstance)next(NULL, "vkCreateInstance");
    VkResult result = create(&copy, allocator, out);
    if (result != VK_SUCCESS)
        return result;
    pthread_mutex_lock(&lock);
    instance.key = key_of(*out);
    instance.instance = *out;
    instance.api = app.apiVersion;
    instance.next_gipa = next;
    instance.destroy_instance = (PFN_vkDestroyInstance)next(*out, "vkDestroyInstance");
    instance.physical_properties = (PFN_vkGetPhysicalDeviceProperties)next(*out, "vkGetPhysicalDeviceProperties");
    instance.physical_features = (PFN_vkGetPhysicalDeviceFeatures2)next(*out, "vkGetPhysicalDeviceFeatures2");
    instance.device_extensions = (PFN_vkEnumerateDeviceExtensionProperties)next(*out, "vkEnumerateDeviceExtensionProperties");
    pthread_mutex_unlock(&lock);
    return result;
}

static VKAPI_ATTR void VKAPI_CALL DestroyInstance(VkInstance i, const VkAllocationCallbacks *allocator)
{
    PFN_vkDestroyInstance destroy = instance.key == key_of(i) ? instance.destroy_instance : NULL;
    if (destroy)
        memset(&instance, 0, sizeof(instance));
    if (destroy)
        destroy(i, allocator);
}

// ---- dispatch ------------------------------------------------------------------

static PFN_vkVoidFunction device_hook(const char *name)
{
#define HOOK(fn) if (!strcmp(name, "vk" #fn)) return (PFN_vkVoidFunction)fn
    HOOK(DestroyDevice);
    HOOK(CreateBuffer);
    HOOK(DestroyBuffer);
    HOOK(AllocateMemory);
    HOOK(CmdDispatch);
    HOOK(CmdDispatchIndirect);
    HOOK(CmdBeginRenderPass);
    HOOK(CmdCopyBuffer);
    HOOK(CmdCopyBufferToImage);
    HOOK(CreateShaderModule);
    HOOK(DestroyShaderModule);
    HOOK(CreateGraphicsPipelines);
    HOOK(DestroyPipeline);
    HOOK(CreateDescriptorSetLayout);
    HOOK(CmdBindPipeline);
    HOOK(CmdDraw);
    HOOK(CmdDrawIndirect);
    HOOK(CmdPipelineBarrier);
    if (!strcmp(name, "vkCmdInsertDebugUtilsLabelEXT")) return (PFN_vkVoidFunction)CmdInsertDebugUtilsLabel;
#undef HOOK
    return NULL;
}

EXPORT VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL xdv_GetDeviceProcAddr(VkDevice d, const char *name);

EXPORT VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL xdv_GetInstanceProcAddr(VkInstance i, const char *name)
{
    if (!strcmp(name, "vkGetInstanceProcAddr")) return (PFN_vkVoidFunction)xdv_GetInstanceProcAddr;
    if (!strcmp(name, "vkGetDeviceProcAddr")) return (PFN_vkVoidFunction)xdv_GetDeviceProcAddr;
    if (!strcmp(name, "vkCreateInstance")) return (PFN_vkVoidFunction)CreateInstance;
    if (!strcmp(name, "vkDestroyInstance")) return (PFN_vkVoidFunction)DestroyInstance;
    if (!strcmp(name, "vkCreateDevice")) return (PFN_vkVoidFunction)CreateDevice;
    PFN_vkVoidFunction hook = device_hook(name);
    if (hook)
        return hook;
    return i && instance.key == key_of(i) ? instance.next_gipa(i, name) : NULL;
}

EXPORT VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL xdv_GetDeviceProcAddr(VkDevice d, const char *name)
{
    if (!strcmp(name, "vkGetDeviceProcAddr")) return (PFN_vkVoidFunction)xdv_GetDeviceProcAddr;
    if (device.key != key_of(d))
        return NULL;
    PFN_vkVoidFunction hook = device_hook(name);
    if (hook && !strcmp(name, "vkCmdInsertDebugUtilsLabelEXT"))
        return hook;
    if (hook && device.next_gdpa(d, name))
        return hook;
    return device.next_gdpa(d, name);
}

// ---- for the app ---------------------------------------------------------------

/// SDL 3.2's buffer: SDL_GPUBuffer is a VulkanBufferContainer, whose first field is the active
/// VulkanBuffer { VulkanBufferContainer *container; Uint32 containerIndex; VkBuffer buffer; … }.
typedef struct { void *container; uint32_t index; VkBuffer buffer; } SdlBuffer;

/// The GPU address of an SDL_GPUBuffer made with storage usage, or 0. Ask every frame: SDL
/// swaps the buffer underneath when it cycles or defragments, and the address changes with it.
EXPORT uint64_t xdv_address(void *sdl)
{
    if (!sdl)
        return 0;
    SdlBuffer *active = *(SdlBuffer **)sdl;
    if (!active || active->container != sdl)
        return 0;
    pthread_mutex_lock(&lock);
    uint64_t *slot = device.addresses ? find_buffer((uint64_t)active->buffer, 0) : NULL;
    uint64_t address = 0;
    if (slot)
    {
        if (!*slot)
        {
            VkBufferDeviceAddressInfo info = { .sType = VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO, .buffer = active->buffer };
            *slot = device.buffer_address(device.device, &info);
        }
        address = *slot;
        device.handed_out |= address != 0;
    }
    pthread_mutex_unlock(&lock);
    return address;
}

/// What the device was made with, one line each.
EXPORT const char *xdv_report(void) { return report; }

/// Whether the layer is in the device chain (it saw vkCreateDevice).
EXPORT int xdv_active(void) { return device.key != NULL; }

/// Whether storage buffers have GPU addresses (bufferDeviceAddress is on).
EXPORT int xdv_addresses(void) { return device.addresses; }

/// Whether mesh shaders are on: a "vertex" shader whose SPIR-V is a mesh entry point runs as one.
EXPORT int xdv_mesh(void) { return device.mesh; }
