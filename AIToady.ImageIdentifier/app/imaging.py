"""Image loading and preprocessing (Pillow + OpenCV).

For coins this produces three views of each side:
  * the photo cropped to the coin,
  * a CLAHE-enhanced greyscale copy that brings out worn relief and faint letters,
  * a "rim strip": the outer band of the coin unwrapped with a polar transform so the
    circular legend reads as straight text.
"""

import base64
import io
import math
from dataclasses import dataclass
from typing import Optional

import cv2
import numpy as np
from PIL import Image, ImageFilter, ImageOps, UnidentifiedImageError

try:  # Optional: lets the app read iPhone HEIC/HEIF photos.
    from pillow_heif import register_heif_opener

    register_heif_opener()
except ImportError:  # pragma: no cover
    pass

DETECT_SIDE = 600  # coin detection runs on a downscaled copy this size
CROP_MARGIN = 1.04  # crop slightly outside the detected rim
RIM_INNER = 0.6  # rim strip covers radius 0.6r..1.0r, where legends sit
RIM_MAX_WIDTH = 2048


class InvalidImageError(ValueError):
    """Raised when uploaded bytes are not a readable image."""


@dataclass(frozen=True)
class PreparedImage:
    original_b64: str  # whole photo, normalised size and orientation
    coin_b64: str  # cropped to the coin when one was found, otherwise the whole photo
    enhanced_b64: str  # CLAHE-enhanced greyscale copy of coin_b64
    rim_b64: Optional[str]  # unwrapped rim strip, only when a coin was found
    coin_found: bool


def load_image(data: bytes) -> Image.Image:
    try:
        img = Image.open(io.BytesIO(data))
        img.load()
    except (UnidentifiedImageError, OSError, Image.DecompressionBombError) as exc:
        raise InvalidImageError("The uploaded file is not a readable image.") from exc

    img = ImageOps.exif_transpose(img)  # respect phone camera rotation
    if img.mode in ("RGBA", "LA", "P"):
        rgba = img.convert("RGBA")
        background = Image.new("RGB", rgba.size, (255, 255, 255))
        background.paste(rgba, mask=rgba.split()[-1])
        img = background
    elif img.mode != "RGB":
        img = img.convert("RGB")
    return img


def normalise_size(img: Image.Image, min_side: int, max_side: int) -> Image.Image:
    longest = max(img.size)
    if longest > max_side:
        scale = max_side / longest
    elif longest < min_side:
        scale = min_side / longest  # small images: upscale so fine text is legible
    else:
        return img
    new_size = (max(1, round(img.width * scale)), max(1, round(img.height * scale)))
    return img.resize(new_size, Image.Resampling.LANCZOS)


def find_coin(img: Image.Image) -> Optional[tuple[float, float, float]]:
    """Return (centre_x, centre_y, radius) of the most prominent round object, or None."""
    grey = np.asarray(img.convert("L"))
    h, w = grey.shape
    scale = min(1.0, DETECT_SIDE / max(h, w))
    small = cv2.resize(grey, (round(w * scale), round(h * scale)), interpolation=cv2.INTER_AREA) \
        if scale < 1.0 else grey
    blur = cv2.GaussianBlur(small, (7, 7), 0)
    sh, sw = blur.shape
    min_side = min(sh, sw)

    best = None
    _, mask = cv2.threshold(blur, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    kernel = np.ones((9, 9), np.uint8)
    for candidate_mask in (mask, 255 - mask):  # coin may be lighter or darker than background
        closed = cv2.morphologyEx(candidate_mask, cv2.MORPH_CLOSE, kernel, iterations=2)
        contours, _ = cv2.findContours(closed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        for contour in contours:
            x, y, bw, bh = cv2.boundingRect(contour)
            if x <= 1 and y <= 1 and x + bw >= sw - 1 and y + bh >= sh - 1:
                continue  # this is the background, not the coin
            (cx, cy), r = cv2.minEnclosingCircle(contour)
            if r < 0.2 * min_side:
                continue
            roundness = cv2.contourArea(contour) / (math.pi * r * r)
            if roundness < 0.7:  # ancient flans are irregular, so keep this loose
                continue
            score = r * r * roundness
            if best is None or score > best[0]:
                best = (score, cx, cy, r)

    if best is None:
        circles = cv2.HoughCircles(
            blur, cv2.HOUGH_GRADIENT, dp=1.2, minDist=min_side, param1=100, param2=40,
            minRadius=int(0.25 * min_side), maxRadius=int(0.55 * min_side),
        )
        if circles is not None:
            cx, cy, r = circles[0][0]
            best = (0.0, float(cx), float(cy), float(r))

    if best is None:
        return None
    _, cx, cy, r = best
    return cx / scale, cy / scale, r / scale


def crop_to_coin(img: Image.Image, circle: tuple[float, float, float]) -> tuple[Image.Image, tuple[float, float, float]]:
    cx, cy, r = circle
    half = r * CROP_MARGIN
    left, top = max(0, round(cx - half)), max(0, round(cy - half))
    right, bottom = min(img.width, round(cx + half)), min(img.height, round(cy + half))
    cropped = img.crop((left, top, right, bottom))
    return cropped, (cx - left, cy - top, r)


def enhance_relief(img: Image.Image) -> Image.Image:
    grey = np.asarray(ImageOps.grayscale(img))
    clahe = cv2.createCLAHE(clipLimit=3.0, tileGridSize=(8, 8))
    enhanced = Image.fromarray(clahe.apply(grey))
    enhanced = enhanced.filter(ImageFilter.UnsharpMask(radius=2, percent=160, threshold=3))
    return enhanced.convert("RGB")


def rim_strip(img: Image.Image, circle: tuple[float, float, float]) -> Image.Image:
    """Unwrap the coin's outer band into two stacked rows that read clockwise.

    Row 1 covers 0-200 degrees and row 2 covers 180-380 degrees (clockwise from 3 o'clock),
    so a legend that crosses the starting point is still complete in one of the rows.
    """
    cx, cy, r = circle
    arr = np.asarray(img)
    angular = int(min(max(2 * math.pi * r, 720), 4096))
    radial = max(int(r), 64)
    # warpPolar: rows are angle (clockwise on screen, from 3 o'clock), columns are radius.
    polar = cv2.warpPolar(arr, (radial, angular), (cx, cy), r,
                          cv2.WARP_POLAR_LINEAR + cv2.INTER_CUBIC)
    band = polar[:, int(radial * RIM_INNER):]
    # Rows become radius with the rim at the top, columns become angle left to right,
    # so letters whose tops point outward appear upright.
    strip = np.ascontiguousarray(np.flipud(band.transpose(1, 0, 2)))

    n = strip.shape[1]
    segment = int(n * 200 / 360)
    start = int(n * 180 / 360)
    first = strip[:, :segment]
    second = np.concatenate([strip, strip], axis=1)[:, start:start + segment]
    gap = np.full((8, segment, 3), 255, dtype=np.uint8)
    out = Image.fromarray(np.vstack([first, gap, second]))
    if out.width > RIM_MAX_WIDTH:
        height = max(1, round(out.height * RIM_MAX_WIDTH / out.width))
        out = out.resize((RIM_MAX_WIDTH, height), Image.Resampling.LANCZOS)
    return out


def to_jpeg_b64(img: Image.Image, quality: int = 92) -> str:
    buffer = io.BytesIO()
    img.save(buffer, format="JPEG", quality=quality, optimize=True)
    return base64.b64encode(buffer.getvalue()).decode("ascii")


def prepare(data: bytes, min_side: int, max_side: int) -> PreparedImage:
    original = normalise_size(load_image(data), min_side, max_side)
    original_b64 = to_jpeg_b64(original)

    circle = find_coin(original)
    if circle is None:
        return PreparedImage(original_b64, original_b64, to_jpeg_b64(enhance_relief(original)),
                             None, False)

    coin, local_circle = crop_to_coin(original, circle)
    scale = 1.0
    longest = max(coin.size)
    if longest < min_side or longest > max_side:
        resized = normalise_size(coin, min_side, max_side)
        scale = resized.width / coin.width
        coin = resized
    cx, cy, r = local_circle
    local_circle = (cx * scale, cy * scale, r * scale)

    return PreparedImage(
        original_b64=original_b64,
        coin_b64=to_jpeg_b64(coin),
        enhanced_b64=to_jpeg_b64(enhance_relief(coin)),
        rim_b64=to_jpeg_b64(rim_strip(coin, local_circle)),
        coin_found=True,
    )
