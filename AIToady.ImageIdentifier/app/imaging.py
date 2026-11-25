"""Image loading and preprocessing with Pillow."""

import base64
import io
from dataclasses import dataclass

from PIL import Image, ImageFilter, ImageOps, UnidentifiedImageError

try:  # Optional: lets the app read iPhone HEIC/HEIF photos.
    from pillow_heif import register_heif_opener

    register_heif_opener()
except ImportError:  # pragma: no cover
    pass


class InvalidImageError(ValueError):
    """Raised when uploaded bytes are not a readable image."""


@dataclass(frozen=True)
class PreparedImage:
    standard_b64: str  # colour image, normalised size and orientation
    enhanced_b64: str  # high-contrast, sharpened greyscale copy for reading inscriptions
    width: int
    height: int


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


def enhance_for_text(img: Image.Image) -> Image.Image:
    grey = ImageOps.grayscale(img)
    grey = ImageOps.autocontrast(grey, cutoff=1)
    grey = ImageOps.equalize(grey)
    grey = grey.filter(ImageFilter.UnsharpMask(radius=2, percent=160, threshold=3))
    return grey.convert("RGB")


def to_jpeg_b64(img: Image.Image, quality: int = 92) -> str:
    buffer = io.BytesIO()
    img.save(buffer, format="JPEG", quality=quality, optimize=True)
    return base64.b64encode(buffer.getvalue()).decode("ascii")


def prepare(data: bytes, min_side: int, max_side: int) -> PreparedImage:
    img = normalise_size(load_image(data), min_side, max_side)
    return PreparedImage(
        standard_b64=to_jpeg_b64(img),
        enhanced_b64=to_jpeg_b64(enhance_for_text(img)),
        width=img.width,
        height=img.height,
    )
