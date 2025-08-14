/*
 * GLSL Color Library - Based on OpenTK.Graphics.Color4
 * -------------------------------------------------------
 * This library provides a set of predefined color constants
 * for use in GLSL shaders. The colors and their RGBA values
 * are sourced directly from the OpenTK library's Color4 struct.
 *
 * All colors are defined as `const vec4` for compile-time efficiency.
 * The alpha component is 1.0 for all colors except Transparent.
 *
 * Naming convention: PascalCase -> UPPER_SNAKE_CASE
 * Example: OpenTK's `CornflowerBlue` is `CORNFLOWER_BLUE`.
 */

// A value of (0,0,0,0)
const vec4 TRANSPARENT = vec4(0.0, 0.0, 0.0, 0.0);

//-------------------------------------------------
// Primary & Common Colors
//-------------------------------------------------
const vec4 BLACK         = vec4(0.0, 0.0, 0.0, 1.0);
const vec4 WHITE         = vec4(1.0, 1.0, 1.0, 1.0);
const vec4 RED           = vec4(1.0, 0.0, 0.0, 1.0);
const vec4 GREEN         = vec4(0.0, 1.0, 0.0, 1.0);
const vec4 BLUE          = vec4(0.0, 0.0, 1.0, 1.0);
const vec4 YELLOW        = vec4(1.0, 1.0, 0.0, 1.0);
const vec4 CYAN          = vec4(0.0, 1.0, 1.0, 1.0); // Also known as Aqua
const vec4 MAGENTA       = vec4(1.0, 0.0, 1.0, 1.0); // Also known as Fuchsia
const vec4 GRAY          = vec4(0.5, 0.5, 0.5, 1.0);


//-------------------------------------------------
// Full Alphabetical List (X11/Web Colors)
//-------------------------------------------------
const vec4 ALICE_BLUE            = vec4(0.94, 0.97, 1.0, 1.0);
const vec4 ANTIQUE_WHITE         = vec4(0.98, 0.92, 0.84, 1.0);
const vec4 AQUA                    = vec4(0.0, 1.0, 1.0, 1.0);
const vec4 AQUAMARINE              = vec4(0.5, 1.0, 0.83, 1.0);
const vec4 AZURE                   = vec4(0.94, 1.0, 1.0, 1.0);
const vec4 BEIGE                   = vec4(0.96, 0.96, 0.86, 1.0);
const vec4 BISQUE                  = vec4(1.0, 0.89, 0.77, 1.0);
const vec4 BLANCHED_ALMOND         = vec4(1.0, 0.92, 0.8, 1.0);
const vec4 BLUE_VIOLET             = vec4(0.54, 0.17, 0.89, 1.0);
const vec4 BROWN                   = vec4(0.65, 0.16, 0.16, 1.0);
const vec4 BURLY_WOOD              = vec4(0.87, 0.72, 0.53, 1.0);
const vec4 CADET_BLUE              = vec4(0.37, 0.62, 0.63, 1.0);
const vec4 CHARTREUSE              = vec4(0.5, 1.0, 0.0, 1.0);
const vec4 CHOCOLATE               = vec4(0.82, 0.41, 0.12, 1.0);
const vec4 CORAL                   = vec4(1.0, 0.5, 0.31, 1.0);
const vec4 CORNFLOWER_BLUE         = vec4(0.39, 0.58, 0.93, 1.0);
const vec4 CORNSILK                = vec4(1.0, 0.97, 0.86, 1.0);
const vec4 CRIMSON                 = vec4(0.86, 0.08, 0.24, 1.0);
const vec4 DARK_BLUE               = vec4(0.0, 0.0, 0.55, 1.0);
const vec4 DARK_CYAN               = vec4(0.0, 0.55, 0.55, 1.0);
const vec4 DARK_GOLDENROD          = vec4(0.72, 0.53, 0.04, 1.0);
const vec4 DARK_GRAY               = vec4(0.66, 0.66, 0.66, 1.0);
const vec4 DARK_GREEN              = vec4(0.0, 0.39, 0.0, 1.0);
const vec4 DARK_KHAKI              = vec4(0.74, 0.72, 0.42, 1.0);
const vec4 DARK_MAGENTA            = vec4(0.55, 0.0, 0.55, 1.0);
const vec4 DARK_OLIVE_GREEN        = vec4(0.33, 0.42, 0.18, 1.0);
const vec4 DARK_ORANGE             = vec4(1.0, 0.55, 0.0, 1.0);
const vec4 DARK_ORCHID             = vec4(0.6, 0.2, 0.8, 1.0);
const vec4 DARK_RED                = vec4(0.55, 0.0, 0.0, 1.0);
const vec4 DARK_SALMON             = vec4(0.91, 0.59, 0.48, 1.0);
const vec4 DARK_SEA_GREEN          = vec4(0.56, 0.74, 0.56, 1.0);
const vec4 DARK_SLATE_BLUE         = vec4(0.28, 0.24, 0.55, 1.0);
const vec4 DARK_SLATE_GRAY         = vec4(0.18, 0.31, 0.31, 1.0);
const vec4 DARK_TURQUOISE          = vec4(0.0, 0.81, 0.82, 1.0);
const vec4 DARK_VIOLET             = vec4(0.58, 0.0, 0.83, 1.0);
const vec4 DEEP_PINK               = vec4(1.0, 0.08, 0.58, 1.0);
const vec4 DEEP_SKY_BLUE           = vec4(0.0, 0.75, 1.0, 1.0);
const vec4 DIM_GRAY                = vec4(0.41, 0.41, 0.41, 1.0);
const vec4 DODGER_BLUE             = vec4(0.12, 0.56, 1.0, 1.0);
const vec4 FIREBRICK               = vec4(0.7, 0.13, 0.13, 1.0);
const vec4 FLORAL_WHITE            = vec4(1.0, 0.98, 0.94, 1.0);
const vec4 FOREST_GREEN            = vec4(0.13, 0.55, 0.13, 1.0);
const vec4 FUCHSIA                 = vec4(1.0, 0.0, 1.0, 1.0);
const vec4 GAINSBORO               = vec4(0.86, 0.86, 0.86, 1.0);
const vec4 GHOST_WHITE             = vec4(0.97, 0.97, 1.0, 1.0);
const vec4 GOLD                    = vec4(1.0, 0.84, 0.0, 1.0);
const vec4 GOLDENROD               = vec4(0.85, 0.65, 0.13, 1.0);
const vec4 GREEN_YELLOW            = vec4(0.68, 1.0, 0.18, 1.0);
const vec4 HONEYDEW                = vec4(0.94, 1.0, 0.94, 1.0);
const vec4 HOT_PINK                = vec4(1.0, 0.41, 0.71, 1.0);
const vec4 INDIAN_RED              = vec4(0.8, 0.36, 0.36, 1.0);
const vec4 INDIGO                  = vec4(0.29, 0.0, 0.51, 1.0);
const vec4 IVORY                   = vec4(1.0, 1.0, 0.94, 1.0);
const vec4 KHAKI                   = vec4(0.94, 0.9, 0.55, 1.0);
const vec4 LAVENDER                = vec4(0.9, 0.9, 0.98, 1.0);
const vec4 LAVENDER_BLUSH          = vec4(1.0, 0.94, 0.96, 1.0);
const vec4 LAWN_GREEN              = vec4(0.49, 0.99, 0.0, 1.0);
const vec4 LEMON_CHIFFON           = vec4(1.0, 0.98, 0.8, 1.0);
const vec4 LIGHT_BLUE              = vec4(0.68, 0.85, 0.9, 1.0);
const vec4 LIGHT_CORAL             = vec4(0.94, 0.5, 0.5, 1.0);
const vec4 LIGHT_CYAN              = vec4(0.88, 1.0, 1.0, 1.0);
const vec4 LIGHT_GOLDENROD_YELLOW  = vec4(0.98, 0.98, 0.82, 1.0);
const vec4 LIGHT_GRAY              = vec4(0.83, 0.83, 0.83, 1.0);
const vec4 LIGHT_GREEN             = vec4(0.56, 0.93, 0.56, 1.0);
const vec4 LIGHT_PINK              = vec4(1.0, 0.71, 0.76, 1.0);
const vec4 LIGHT_SALMON            = vec4(1.0, 0.63, 0.48, 1.0);
const vec4 LIGHT_SEA_GREEN         = vec4(0.13, 0.7, 0.67, 1.0);
const vec4 LIGHT_SKY_BLUE          = vec4(0.53, 0.81, 0.98, 1.0);
const vec4 LIGHT_SLATE_GRAY        = vec4(0.47, 0.53, 0.6, 1.0);
const vec4 LIGHT_STEEL_BLUE        = vec4(0.69, 0.77, 0.87, 1.0);
const vec4 LIGHT_YELLOW            = vec4(1.0, 1.0, 0.88, 1.0);
const vec4 LIME                    = vec4(0.0, 1.0, 0.0, 1.0);
const vec4 LIME_GREEN              = vec4(0.2, 0.8, 0.2, 1.0);
const vec4 LINEN                   = vec4(0.98, 0.94, 0.9, 1.0);
const vec4 MAROON                  = vec4(0.5, 0.0, 0.0, 1.0);
const vec4 MEDIUM_AQUAMARINE       = vec4(0.4, 0.8, 0.67, 1.0);
const vec4 MEDIUM_BLUE             = vec4(0.0, 0.0, 0.8, 1.0);
const vec4 MEDIUM_ORCHID           = vec4(0.73, 0.33, 0.83, 1.0);
const vec4 MEDIUM_PURPLE           = vec4(0.58, 0.44, 0.86, 1.0);
const vec4 MEDIUM_SEA_GREEN        = vec4(0.24, 0.7, 0.44, 1.0);
const vec4 MEDIUM_SLATE_BLUE       = vec4(0.48, 0.41, 0.93, 1.0);
const vec4 MEDIUM_SPRING_GREEN     = vec4(0.0, 0.98, 0.6, 1.0);
const vec4 MEDIUM_TURQUOISE        = vec4(0.28, 0.82, 0.8, 1.0);
const vec4 MEDIUM_VIOLET_RED       = vec4(0.78, 0.08, 0.52, 1.0);
const vec4 MIDNIGHT_BLUE           = vec4(0.1, 0.1, 0.44, 1.0);
const vec4 MINT_CREAM              = vec4(0.96, 1.0, 0.98, 1.0);
const vec4 MISTY_ROSE              = vec4(1.0, 0.89, 0.88, 1.0);
const vec4 MOCCASIN                = vec4(1.0, 0.89, 0.71, 1.0);
const vec4 NAVAJO_WHITE            = vec4(1.0, 0.87, 0.68, 1.0);
const vec4 NAVY                    = vec4(0.0, 0.0, 0.5, 1.0);
const vec4 OLD_LACE                = vec4(0.99, 0.96, 0.9, 1.0);
const vec4 OLIVE                   = vec4(0.5, 0.5, 0.0, 1.0);
const vec4 OLIVE_DRAB              = vec4(0.42, 0.56, 0.14, 1.0);
const vec4 ORANGE                  = vec4(1.0, 0.65, 0.0, 1.0);
const vec4 ORANGE_RED              = vec4(1.0, 0.27, 0.0, 1.0);
const vec4 ORCHID                  = vec4(0.85, 0.44, 0.84, 1.0);
const vec4 PALE_GOLDENROD          = vec4(0.93, 0.91, 0.67, 1.0);
const vec4 PALE_GREEN              = vec4(0.6, 0.98, 0.6, 1.0);
const vec4 PALE_TURQUOISE          = vec4(0.69, 0.93, 0.93, 1.0);
const vec4 PALE_VIOLET_RED         = vec4(0.86, 0.44, 0.58, 1.0);
const vec4 PAPAYA_WHIP             = vec4(1.0, 0.94, 0.84, 1.0);
const vec4 PEACH_PUFF              = vec4(1.0, 0.85, 0.73, 1.0);
const vec4 PERU                    = vec4(0.8, 0.52, 0.25, 1.0);
const vec4 PINK                    = vec4(1.0, 0.75, 0.8, 1.0);
const vec4 PLUM                    = vec4(0.87, 0.63, 0.87, 1.0);
const vec4 POWDER_BLUE             = vec4(0.69, 0.88, 0.9, 1.0);
const vec4 PURPLE                  = vec4(0.5, 0.0, 0.5, 1.0);
const vec4 ROSY_BROWN              = vec4(0.74, 0.56, 0.56, 1.0);
const vec4 ROYAL_BLUE              = vec4(0.25, 0.41, 0.88, 1.0);
const vec4 SADDLE_BROWN            = vec4(0.55, 0.27, 0.07, 1.0);
const vec4 SALMON                  = vec4(0.98, 0.5, 0.45, 1.0);
const vec4 SANDY_BROWN             = vec4(0.96, 0.64, 0.38, 1.0);
const vec4 SEA_GREEN               = vec4(0.18, 0.55, 0.34, 1.0);
const vec4 SEA_SHELL               = vec4(1.0, 0.96, 0.93, 1.0);
const vec4 SIENNA                  = vec4(0.63, 0.32, 0.18, 1.0);
const vec4 SILVER                  = vec4(0.75, 0.75, 0.75, 1.0);
const vec4 SKY_BLUE                = vec4(0.53, 0.81, 0.92, 1.0);
const vec4 SLATE_BLUE              = vec4(0.42, 0.35, 0.8, 1.0);
const vec4 SLATE_GRAY              = vec4(0.44, 0.5, 0.56, 1.0);
const vec4 SNOW                    = vec4(1.0, 0.98, 0.98, 1.0);
const vec4 SPRING_GREEN            = vec4(0.0, 1.0, 0.5, 1.0);
const vec4 STEEL_BLUE              = vec4(0.27, 0.51, 0.71, 1.0);
const vec4 TAN                     = vec4(0.82, 0.71, 0.55, 1.0);
const vec4 TEAL                    = vec4(0.0, 0.5, 0.5, 1.0);
const vec4 THISTLE                 = vec4(0.85, 0.75, 0.85, 1.0);
const vec4 TOMATO                  = vec4(1.0, 0.39, 0.28, 1.0);
const vec4 TURQUOISE               = vec4(0.25, 0.88, 0.82, 1.0);
const vec4 VIOLET                  = vec4(0.93, 0.51, 0.93, 1.0);
const vec4 WHEAT                   = vec4(0.96, 0.87, 0.7, 1.0);
const vec4 WHITE_SMOKE             = vec4(0.96, 0.96, 0.96, 1.0);
const vec4 YELLOW_GREEN            = vec4(0.6, 0.8, 0.2, 1.0);