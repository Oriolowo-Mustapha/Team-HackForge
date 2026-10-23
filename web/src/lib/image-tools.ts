"use client";

/**
 * Downscales a photo to at most `maxSide` pixels and re-encodes it as JPEG, so uploads stay small on
 * slow rural connections. EXIF orientation is applied by the browser when decoding. If the browser
 * can't decode the format (e.g. HEIC on desktop), the original file is returned unchanged and the
 * server decides whether it is acceptable.
 */
export async function compressImage(file: Blob, maxSide = 1600, quality = 0.82): Promise<Blob> {
  try {
    const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
    const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
    const width = Math.round(bitmap.width * scale);
    const height = Math.round(bitmap.height * scale);
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d");
    if (!context) return file;
    context.drawImage(bitmap, 0, 0, width, height);
    bitmap.close();
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", quality));
    return blob && blob.size < file.size ? blob : file.type === "image/jpeg" ? file : (blob ?? file);
  } catch {
    return file;
  }
}

export const ACCEPTED_IMAGE_TYPES = "image/jpeg,image/png,image/webp,image/heic,image/heif";
export const MAX_IMAGE_BYTES = 8 * 1024 * 1024;
