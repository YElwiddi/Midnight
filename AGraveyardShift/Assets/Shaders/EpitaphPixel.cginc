// Screen-locked pixel grid shared by the menu's pixel shaders ("UI/Epitaph Pixel" for images,
// "TextMeshPro/Mobile/Distance Field Pixel" for text, "UI/Epitaph Sweep" for the splash logo; see
// PixelCanvas). Every fragment samples at the centre of the virtual pixel it falls in, so all text
// and art on the canvas read as one low-resolution image on the same grid, whatever their size.
#ifndef EPITAPH_PIXEL_INCLUDED
#define EPITAPH_PIXEL_INCLUDED

// Size of one virtual pixel in screen pixels for a virtual vertical resolution of `lines`
// (whole pixels so the grid stays even; lines <= 0 turns the effect off).
float EpitaphPixelCell(float lines)
{
    return lines > 0 ? max(1.0, floor(_ScreenParams.y / lines + 0.5)) : 1.0;
}

// Offset (screen pixels) from this fragment to the centre of its virtual pixel. Shaders move their
// UVs by it along ddx/ddy (UI quads are affine on screen) to sample where the pixel's centre is.
float2 EpitaphPixelDelta(float2 fragPos, float cell)
{
    return (floor(fragPos / cell) + 0.5) * cell - fragPos;
}

// The texture averaged over a whole virtual pixel (4x4 taps around uv, its centre), so fine detail
// shrinks instead of sparkling. dx/dy are the UV steps per screen pixel (ddx/ddy of the UV).
half4 EpitaphPixelArea(sampler2D tex, float2 uv, float2 dx, float2 dy, float cell)
{
    half4 sum = 0;
    for (int y = 0; y < 4; y++)
    {
        for (int x = 0; x < 4; x++)
        {
            float2 o = (float2(x, y) - 1.5) * (cell / 4.0);
            sum += tex2Dlod(tex, float4(uv + dx * o.x + dy * o.y, 0, 0));
        }
    }
    return sum / 16.0;
}

// 4x4 ordered-dither threshold (0..1) for a virtual pixel.
float EpitaphBayer4(float2 virtualPixel)
{
    const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    int2 p = int2(fmod(virtualPixel, 4.0));
    return (m[p.y * 4 + p.x] + 0.5) / 16.0;
}

// Quantises a 0..1 value to `steps` levels (0 = leave smooth), optionally ordered-dithered between levels.
float EpitaphPixelSteps(float v, float steps, float dither, float2 fragPos, float cell)
{
    if (steps <= 0) return v;
    float t = lerp(0.5, EpitaphBayer4(floor(fragPos / cell)), dither);
    return saturate(floor(v * steps + t) / steps);
}
#endif
