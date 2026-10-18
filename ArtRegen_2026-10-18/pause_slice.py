from PIL import Image
import numpy as np

SRC = '/Users/edwin/Documents/PROJECTS/Unity/BorrowTomorrow/Assets/Game/Art/UI/UI_PauseMenuPanel.png'
OUT = '/Users/edwin/Documents/PROJECTS/Unity/BorrowTomorrow/Assets/Game/Art/UI/'
orig = np.asarray(Image.open(SRC).convert('RGBA')).astype(np.float32)
H, W = orig.shape[:2]

# ---------- 1. clean paper ----------
X0, X1 = 200, 1550
Y0, Y1 = 225, 2072
bands = [(295, 428), (702, 746), (1008, 1060), (1318, 1370), (1626, 1680), (1866, 1904)]
OV = 30  # crossfade overlap


def band(i, k):
    a, b = bands[i]
    p = orig[a:b, X0:X1].copy()
    if k % 2 == 1:
        p = p[:, ::-1]
    if (k // 2) % 2 == 1:
        p = p[::-1]
    return p


need = (Y1 - Y0) + 40
strip = None
k = 0
order = [0, 3, 1, 5, 2, 4]
while strip is None or strip.shape[0] < need:
    p = band(order[k % len(order)], k)
    k += 1
    if strip is None:
        strip = p
        continue
    w = np.linspace(0, 1, OV)[:, None, None]
    blend = strip[-OV:] * (1 - w) + p[:OV] * w
    strip = np.concatenate([strip[:-OV], blend, p[OV:]], 0)
fill = strip[:Y1 - Y0].copy()
ref = orig[295:428, X0:X1, :3].reshape(-1, 3).mean(0)
rm = fill[..., :3].mean(1, keepdims=True)
fill[..., :3] = fill[..., :3] - rm + ref

clean = orig.copy()
ys = np.arange(Y0, Y1)
xs = np.arange(X0, X1)
ay = np.clip(np.minimum(ys - Y0, Y1 - 1 - ys) / 12.0, 0, 1)
ax = np.clip(np.minimum((xs - X0) / 22.0, (X1 - 1 - xs) / 18.0), 0, 1)
m = (ay[:, None] * ax[None, :])[..., None]
clean[Y0:Y1, X0:X1, :3] = orig[Y0:Y1, X0:X1, :3] * (1 - m) + fill[..., :3] * m

BX0, BX1, BY0, BY1 = 103, 1645, 105, 2213
Image.fromarray(clean[BY0:BY1, BX0:BX1].clip(0, 255).astype(np.uint8)).save(OUT + 'UI_PausePaper.png')


# ---------- matting helper ----------
def lum(a):
    return a[..., :3].mean(-1)


def matte(fg, bg, core):
    d = lum(bg) - lum(fg)
    diff = np.abs(fg[..., :3] - bg[..., :3]).max(-1)
    a = np.clip((np.maximum(d, diff * 0.8) - 10) / 45.0, 0, 1)
    a = np.maximum(a, core)
    a3 = np.maximum(a, 1e-3)[..., None]
    col = (fg[..., :3] - (1 - a)[..., None] * bg[..., :3]) / a3
    out = np.zeros(fg.shape, np.float32)
    out[..., :3] = col.clip(0, 255)
    out[..., 3] = a * 255
    return out


def rrect(h, w, x0, y0, x1, y1, r):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    cx = np.clip(xx, x0 + r, x1 - r)
    cy = np.clip(yy, y0 + r, y1 - r)
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    return np.clip(r - dist + 0.5, 0, 1)


def save(arr, name):
    a = arr[..., 3]
    ys, xs = np.where(a > 8)
    arr = arr[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    Image.fromarray(arr.clip(0, 255).astype(np.uint8)).save(OUT + name)
    print(name, arr.shape)


# ---------- 2. plank ----------
px0, px1, py0, py1 = 205, 1545, 425, 712
fg = orig[py0:py1, px0:px1]
bg = clean[py0:py1, px0:px1]
h, w = fg.shape[:2]
core = rrect(h, w, 228 - px0 + 6, 441 - py0 + 6, 1521 - px0 - 6, 690 - py0 - 6, 50)
plank = matte(fg, bg, core)
plank[..., 3] *= rrect(h, w, 0, 0, w - 1, h - 1, 60)
save(plank, 'UI_PausePlank.png')

# ---------- 3. slider track (slider1 with knob area replaced from slider2) ----------
OFF = 201
tx0, tx1, ty0, ty1 = 225, 1520, 1734, 1850
track = orig[ty0:ty1, tx0:tx1].copy()
kx0, kx1 = 405 - tx0, 600 - tx0
SH = 320
L0, L1 = 400, 650  # replaced span (feathered at both ends)
fw = np.clip(np.minimum(np.arange(L1 - L0), L1 - L0 - 1 - np.arange(L1 - L0)) / 24.0, 0, 1)[None, :, None]
fw[:, 30:L1 - L0 - 30] = 1
track[:, L0 - tx0:L1 - tx0] = track[:, L0 - tx0:L1 - tx0] * (1 - fw) + orig[ty0:ty1, L0 + SH:L1 + SH] * fw
bgt = clean[ty0:ty1, tx0:tx1]
h, w = track.shape[:2]
core = rrect(h, w, 249 - tx0 + 4, 1746 - ty0 + 4, 1500 - tx0 - 4, 1832 - ty0 - 4, 38)
tr = matte(track, bgt, core)
tr[..., 3] *= rrect(h, w, 0, 0, w - 1, h - 1, 45)
save(tr, 'UI_PauseSliderTrack.png')

# ---------- 4. knob ----------
bgk = clean.copy()
bgk[ty0:ty1, 405:600] = orig[ty0 + OFF:ty1 + OFF, 405:600]
kx0, kx1, ky0, ky1 = 420, 610, 1688, 1868
fg = orig[ky0:ky1, kx0:kx1]
bk = bgk[ky0:ky1, kx0:kx1]
h, w = fg.shape[:2]
shape = rrect(h, w, 433 - kx0, 1699 - ky0, 594 - kx0, 1856 - ky0, 30)
kn = matte(fg, bk, shape)
kn[..., 3] *= np.clip(shape + 0.0, 0, 1)
save(kn, 'UI_PauseSliderKnob.png')

# ---------- 5. ornament ----------
ox0, ox1, oy0, oy1 = 240, 1510, 228, 298
fg = orig[oy0:oy1, ox0:ox1]
bo = clean[oy0:oy1, ox0:ox1].copy()
bo[..., :3] = orig[300:300 + (oy1 - oy0), ox0:ox1, :3]
orn = matte(fg, bo, np.zeros(fg.shape[:2], np.float32))
save(orn, 'UI_PauseOrnament.png')
