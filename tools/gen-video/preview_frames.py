# 抽取视频关键帧用于快速评估。用法: python tools/preview_frames.py <video> <outdir>
import cv2, os, sys

video = sys.argv[1] if len(sys.argv) > 1 else "video_out/worker_walk_03.mp4"
outdir = sys.argv[2] if len(sys.argv) > 2 else "video_out/_preview"

cap = cv2.VideoCapture(video)
n = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
fps = cap.get(cv2.CAP_PROP_FPS)
w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
print("frames:", n, "fps:", fps, "size:", w, h)

os.makedirs(outdir, exist_ok=True)
picks = [0, n // 4, n // 2, (3 * n) // 4, n - 1]
for i, fidx in enumerate(picks):
    cap.set(cv2.CAP_PROP_POS_FRAMES, fidx)
    ok, frame = cap.read()
    if ok:
        cv2.imwrite(os.path.join(outdir, f"p{i}_f{fidx}.png"), frame)
        print("saved frame", fidx)
cap.release()
