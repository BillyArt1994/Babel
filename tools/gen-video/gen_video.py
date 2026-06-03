# H:/Babel/tools/gen_video.py
# Seedance 视频生成工具：提交 -> 轮询 -> 下载。支持文生视频 / 图生视频(首帧)。
# 用法:
#   python tools/gen_video.py            # 用脚本底部配置直接跑
import os, sys, time, json, base64, mimetypes
import requests

BASE = "https://chat-test.q1.com/v1"
KEY = os.environ.get("OPENAI_API_KEY", "sk-c3dc41f246bf59f8e6db79b881cc955ee8a004b6")
HEADERS = {"Authorization": "Bearer " + KEY}


def _img_to_data_uri(path):
    mime = mimetypes.guess_type(path)[0] or "image/png"
    with open(path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode()
    return f"data:{mime};base64,{b64}"


def submit(prompt, model="doubao-seedance-2.0", size="1280x720",
           seconds="5", quality="720p", image_path=None, extra=None):
    payload = {
        "prompt": prompt,
        "model": model,
        "size": size,
        "seconds": str(seconds),
        "quality": quality,
    }
    if image_path:
        payload["first_frame_image"] = _img_to_data_uri(image_path)
    if extra:
        payload.update(extra)
    r = requests.post(BASE + "/videos", headers={**HEADERS, "Content-Type": "application/json"},
                      json=payload, timeout=300)
    print("submit HTTP", r.status_code)
    if r.status_code != 200:
        print(r.text[:1500])
        return None
    d = r.json()
    print("task id:", d.get("id"), "status:", d.get("status"))
    return d.get("id")


def poll(video_id, interval=10, max_tries=60):
    url = BASE + "/videos/" + video_id
    for i in range(max_tries):
        r = requests.get(url, headers=HEADERS, timeout=60)
        if r.status_code != 200:
            print("poll HTTP", r.status_code, r.text[:300])
            return None
        d = r.json()
        st = d.get("status")
        print(f"[{i}] status={st} progress={d.get('progress')}")
        if st in ("completed", "succeeded", "success"):
            return d.get("video_url")
        if st in ("failed", "error"):
            print("ERROR:", d.get("error"))
            return None
        time.sleep(interval)
    print("TIMEOUT")
    return None


def download(url, out_path):
    r = requests.get(url, timeout=180)
    if r.status_code != 200:
        print("download HTTP", r.status_code)
        return False
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "wb") as f:
        f.write(r.content)
    print(f"saved {out_path} ({len(r.content)} bytes)")
    return True


def run(prompt, out_path, **kw):
    image_path = kw.pop("image_path", None)
    vid = submit(prompt, image_path=image_path, **kw)
    if not vid:
        return None
    url = poll(vid)
    if not url:
        return None
    print("video_url:", url)
    download(url, out_path)
    return url


if __name__ == "__main__":
    # 默认配置在调用处覆盖
    print("import this module and call run(); or edit __main__")
