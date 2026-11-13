#ifndef DINO_SHELL_STYLE_GLSL
#define DINO_SHELL_STYLE_GLSL

// Every growth drawn as shells (Rendering/Shells/ShellStyles.cs), seven rows each, written once by ShellRenderer.UploadStyles: the layout must match it.
const int SHELL_STYLES = 16;    // ShellStyles.Most
const int SHELL_ROWS = 7;

const int SHELL_MASK_VERTEX = 0;    // ShellMask
const int SHELL_MASK_ROCK = 1;
const int SHELL_MASK_GROUND = 2;

layout(std140, binding = 13) uniform ShellData {
    // 0: length (m), clump cell (m), clump width (share of a cell), clump fill
    // 1: carpet, height variety, shape (power of a dome), stretch along the comb
    // 2: fringe (share of the length), strand size (m), variety of shade, 1 for fur and 0 for moss
    // 3: root shade, root occlusion, translucency, roughness
    // 4: tip tint (rgb), porosity
    // 5: lean, droop, sway (in lengths), mask (ShellMask)
    // 6: crease (how dark it gets between clumps, 0..1), unused, unused, unused
    vec4 shellStyles[SHELL_STYLES * SHELL_ROWS];
};

vec4 shellRow(int style, int row)
{
    return shellStyles[style * SHELL_ROWS + row];
}

#endif
