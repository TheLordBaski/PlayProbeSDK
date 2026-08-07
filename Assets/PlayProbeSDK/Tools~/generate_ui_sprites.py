#!/usr/bin/env python3
"""Generates the PlayProbe SDK's UI sprite set.

Writes nine white-with-alpha PNGs into Assets/unity-sdk/Textures/UI, each with a .meta carrying the
right Unity import settings — including the 9-slice border, so nothing has to be typed into the
Sprite Editor by hand.

    python3 Assets/unity-sdk/Tools~/generate_ui_sprites.py

Re-running is safe: PNGs are overwritten, but an existing .meta is left alone so the asset GUID (and
therefore every reference to it) survives.

The shapes are rasterised from signed distance fields rather than drawn with a graphics library.
That keeps the script dependency-free — standard library only — and an SDF gives exact analytic
antialiasing at the edge instead of the stair-stepping you get from supersampling a scanline fill.

Every pixel is pure white; only the alpha channel varies. Fully transparent pixels are white too,
which is what stops dark fringing when Unity filters the texture.
"""

import hashlib
import math
import os
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
PACKAGE = os.path.dirname(HERE)
OUT_DIR = os.path.join(PACKAGE, "Textures", "UI")


# --------------------------------------------------------------------------- SDF primitives

def sd_rounded_rect(px, py, cx, cy, hx, hy, r):
    """Distance to a rounded rectangle centred at (cx, cy) with half extents (hx, hy)."""
    r = min(r, hx, hy)
    qx = abs(px - cx) - (hx - r)
    qy = abs(py - cy) - (hy - r)
    outside = math.hypot(max(qx, 0.0), max(qy, 0.0))
    inside = min(max(qx, qy), 0.0)
    return outside + inside - r


def sd_circle(px, py, cx, cy, r):
    return math.hypot(px - cx, py - cy) - r


def sd_segment(px, py, ax, ay, bx, by, r):
    """Distance to a capsule: the segment a->b thickened by r."""
    pax, pay = px - ax, py - ay
    bax, bay = bx - ax, by - ay
    denom = bax * bax + bay * bay
    h = 0.0 if denom == 0 else max(0.0, min(1.0, (pax * bax + pay * bay) / denom))
    return math.hypot(pax - bax * h, pay - bay * h) - r


def to_ring(distance, stroke):
    """Turns a filled shape's distance into a stroke of `stroke` px lying just inside its edge."""
    return abs(distance + stroke * 0.5) - stroke * 0.5


# --------------------------------------------------------------------------- rasteriser

def rasterise(size, distance_fn):
    """White RGBA bytes whose alpha is the shape's antialiased coverage.

    Coverage is `0.5 - d` clamped to [0,1]: a pixel whose centre sits exactly on the edge is half
    covered, and the transition spans roughly one pixel. That is the standard SDF-to-coverage
    approximation and it is accurate enough for shapes with no detail finer than a pixel.
    """
    pixels = bytearray(size * size * 4)

    for y in range(size):
        for x in range(size):
            d = distance_fn(x + 0.5, y + 0.5)
            alpha = min(1.0, max(0.0, 0.5 - d))
            index = (y * size + x) * 4
            pixels[index] = 255
            pixels[index + 1] = 255
            pixels[index + 2] = 255
            pixels[index + 3] = int(alpha * 255 + 0.5)

    return bytes(pixels)


def write_png(path, size, pixels):
    """Minimal PNG writer: 8-bit RGBA, one IDAT, filter type 0 on every row."""
    raw = bytearray()
    stride = size * 4
    for y in range(size):
        raw.append(0)
        raw.extend(pixels[y * stride:(y + 1) * stride])

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")

    with open(path, "wb") as handle:
        handle.write(png)


# --------------------------------------------------------------------------- Unity .meta

META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  spritePackingTag:
  compressionQuality: 100
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: {border}, y: {border}, z: {border}, w: {border}}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def stable_guid(name):
    """A deterministic 32-hex GUID, so a regenerated meta keeps pointing at the same asset."""
    return hashlib.md5(("io.playprobe.sdk/sprites/" + name).encode("utf-8")).hexdigest()


# --------------------------------------------------------------------------- the sprite set

def build_sprites():
    """The set, sized so each shape's source corner is roughly 1.5x the size it renders at.

    Oversizing a sprite is not free: Unity draws UI without mipmaps, so a 94px corner squeezed into
    19px on screen aliases along the arc. Undersizing blurs instead. Around 1.5x is the sweet spot —
    enough headroom for a larger cornerRadius or a high-DPI canvas, without throwing away detail on
    the way down.
    """
    sprites = []

    def rounded(size, radius):
        half = size / 2.0
        return lambda x, y: sd_rounded_rect(x, y, half, half, half, half, radius)

    def rounded_ring(size, radius, stroke):
        fill = rounded(size, radius)
        return lambda x, y: to_ring(fill(x, y), stroke)

    # Rounded rectangle — buttons, inputs, option tiles. Renders at cornerRadius, 10 by default.
    sprites.append(("PlayProbeButton", 64, 16, rounded(64, 16)))
    sprites.append(("PlayProbeButtonOutline", 64, 16, rounded_ring(64, 16, 3)))

    # Softer corner — modal panels and cards. Renders at cornerRadius * 1.6, so 16 by default.
    sprites.append(("PlayProbePanel", 96, 24, rounded(96, 24)))
    sprites.append(("PlayProbePanelOutline", 96, 24, rounded_ring(96, 24, 3)))

    # Capsule — tag chips and the floating feedback button. A circle, so the 9-slice corners are true
    # quarter-circles; PlayProbeCapsuleImage sizes them to half the element's height at runtime, which
    # is the only way one sprite serves a 38px chip and a 56px button and stays round in both.
    #
    # The border stops 2px short of the centre so there is still a middle region to stretch. That far
    # along the arc the curve has all but flattened — it deviates from the tangent by about 0.06px —
    # so the stretched middle reads as perfectly straight.
    sprites.append(("PlayProbePill", 68, 32, rounded(68, 34)))
    sprites.append(("PlayProbePillOutline", 68, 32, rounded_ring(68, 34, 3)))

    # Circle — round icon buttons and dots. Not sliced. Inset half a pixel so the edge antialiases
    # instead of being clipped by the texture bounds.
    sprites.append(("PlayProbeCircle", 64, 0, lambda x, y: sd_circle(x, y, 32, 32, 31.5)))

    # Glyphs. Geometry is authored in a 128px space and scaled down, which keeps the numbers legible.
    u = 64 / 128.0

    def check(x, y):
        return min(sd_segment(x, y, 26 * u, 66 * u, 52 * u, 92 * u, 8 * u),
                   sd_segment(x, y, 52 * u, 92 * u, 102 * u, 36 * u, 8 * u))

    sprites.append(("PlayProbeCheck", 64, 0, check))

    # Speech bubble — a rounded body with a square tail rotated 45 degrees, unioned so the two weld
    # into a single silhouette.
    def bubble(x, y):
        body = sd_rounded_rect(x, y, 64 * u, 54 * u, 50 * u, 36 * u, 16 * u)

        angle = math.radians(45.0)
        dx, dy = x - 44 * u, y - 94 * u
        rx = dx * math.cos(angle) + dy * math.sin(angle)
        ry = -dx * math.sin(angle) + dy * math.cos(angle)
        tail = sd_rounded_rect(rx, ry, 0, 0, 13 * u, 13 * u, 3 * u)

        return min(body, tail)

    sprites.append(("PlayProbeChatBubble", 64, 0, bubble))

    return sprites


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    for name, size, border, distance_fn in build_sprites():
        png_path = os.path.join(OUT_DIR, name + ".png")
        meta_path = png_path + ".meta"

        write_png(png_path, size, rasterise(size, distance_fn))

        if os.path.exists(meta_path):
            print("  {0}.png  ({1}x{1}, border {2})  meta kept".format(name, size, border))
            continue

        with open(meta_path, "w", newline="\n") as handle:
            handle.write(META_TEMPLATE.format(guid=stable_guid(name), border=border))

        print("  {0}.png  ({1}x{1}, border {2})".format(name, size, border))

    print("\nWritten to {0}".format(OUT_DIR))


if __name__ == "__main__":
    main()
