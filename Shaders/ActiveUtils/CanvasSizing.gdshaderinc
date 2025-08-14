varying flat vec2 _canvas_half_size;
varying vec2 _pixel_uv;
varying vec2 pixel_uv_raw;
varying vec2 scale;

#define CANVAS_HALF_SIZE _canvas_half_size
#define PIXEL_UV         _pixel_uv
// ----------------------------------------------------------


// --- New varyings for edge placement (capitalised) -----------------
varying vec2 TOP_LEFT;
varying vec2 TOP_RIGHT;
varying vec2 BOTTOM_LEFT;
varying vec2 BOTTOM_RIGHT;

varying vec2 LEFT_CENTRE;
varying vec2 RIGHT_CENTRE;
varying vec2 TOP_CENTRE;
varying vec2 BOTTOM_CENTRE;

// --- Updated SIZE macro --------------------------------------------
#define SIZE\
    scale = vec2(length(MODEL_MATRIX[0]), length(MODEL_MATRIX[1]));\
    vec2 full_size = VERTEX.xy / (UV - 0.5);\
    _canvas_half_size = full_size * scale * 0.5;\
    pixel_uv_raw = VERTEX.xy;\
    _pixel_uv = VERTEX.xy * scale;\
    TOP_LEFT      = vec2(-_canvas_half_size.x,  _canvas_half_size.y);\
    TOP_RIGHT     =  _canvas_half_size;\
    BOTTOM_LEFT   = -_canvas_half_size;\
    BOTTOM_RIGHT  = vec2( _canvas_half_size.x, -_canvas_half_size.y);\
    LEFT_CENTRE   = vec2(-_canvas_half_size.x, 0.0);\
    RIGHT_CENTRE  = vec2( _canvas_half_size.x, 0.0);\
    TOP_CENTRE    = vec2(0.0,  _canvas_half_size.y);\
    BOTTOM_CENTRE = vec2(0.0, -_canvas_half_size.y);