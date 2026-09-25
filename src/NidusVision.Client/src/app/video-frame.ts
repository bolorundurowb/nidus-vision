/** Downloads the current decoded video frame as a JPEG named for the camera and time. */
export function downloadVideoFrame(video: HTMLVideoElement | undefined, cameraName: string, at: Date): boolean {
  if (!video || video.readyState < HTMLMediaElement.HAVE_CURRENT_DATA || video.videoWidth <= 0 || video.videoHeight <= 0) {
    return false;
  }

  const canvas = document.createElement('canvas');
  canvas.width = video.videoWidth;
  canvas.height = video.videoHeight;
  const context = canvas.getContext('2d');
  if (!context) {
    return false;
  }

  try {
    context.drawImage(video, 0, 0);
    const url = canvas.toDataURL('image/jpeg', 0.92);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${fileSafe(cameraName)} ${formatStamp(at)}.jpg`;
    link.click();
    return true;
  } catch {
    return false;
  }
}

function fileSafe(value: string): string {
  const trimmed = value.trim().replace(/[<>:"/\\|?*]+/g, ' ').replace(/\s+/g, ' ');
  return trimmed || 'Camera';
}

function formatStamp(at: Date): string {
  const pad = (value: number) => value.toString().padStart(2, '0');
  return `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())} ${pad(at.getHours())}-${pad(at.getMinutes())}-${pad(at.getSeconds())}`;
}
