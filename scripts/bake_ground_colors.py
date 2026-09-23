"""Bake satellite-derived ground colour for the imported Risk maps.

The imported maps are stylised geography. This script georeferences each map
(affine + regularised thin-plate spline from imported country positions to
approximate real latitude/longitude), samples NASA Blue Marble Next Generation
(public domain) land colour, and writes a small PNG per map that the ground
shader uses as colour grading. The map's own coastline stays authoritative:
satellite ocean is excluded with a normalised convolution and any map land that
falls on real sea is filled from the nearest satellite land.

Output channels (8-bit PNG, RGBA):
    RGB  sRGB land colour
    A    aridity 0..1 derived from the same colour (linear)

Input (not committed, see THIRD_PARTY_NOTICES.md), NASA Visible Earth:
    references/bluemarble/world.200405.3x5400x2700.jpg
    https://eoimages.gsfc.nasa.gov/images/imagerecords/74000/74042/world.200405.3x5400x2700.jpg
    references/bluemarble/world.200407.3x5400x2700.jpg
    https://eoimages.gsfc.nasa.gov/images/imagerecords/74000/74092/world.200407.3x5400x2700.jpg

Usage: python scripts/bake_ground_colors.py [--preview DIR]
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
MAPS = ROOT / "RiskAI/Assets/RiskAI/Resources/Maps"
# Late spring and high summer are averaged: green northern Iberia and France, dry
# meseta and Anatolia, little seasonal snow outside Greenland, Iceland and the Arctic.
SOURCES = [ROOT / "references/bluemarble/world.200405.3x5400x2700.jpg",
           ROOT / "references/bluemarble/world.200407.3x5400x2700.jpg"]
TEXEL_METRES = 2.56  # one W3E cell

# Approximate real (latitude, longitude) of the region each imported country camp represents.
EUROPE = {
    "Germany": (51.0, 10.4), "Poland": (52.1, 19.4), "Slovakia": (48.7, 19.7), "Czech Republic": (49.8, 15.5),
    "Belarus": (53.7, 28.0), "Slovenia": (46.1, 14.8), "Hungary": (47.2, 19.4), "Austria": (47.6, 14.1),
    "Estonia": (58.7, 25.5), "Latvia": (56.9, 24.6), "Lithuania": (55.3, 23.9), "Kaliningrad": (54.7, 21.2),
    "Northwestern District (Russia)": (61.5, 33.5), "Ukraine": (49.0, 31.4), "Serbia": (44.0, 20.9),
    "North Macedonia": (41.6, 21.7), "Romania": (45.9, 25.0), "Bulgaria": (42.7, 25.5), "Albania": (41.1, 20.0),
    "Croatia": (45.1, 15.2), "Bosnia-Herzegovina": (44.2, 17.8), "Montenegro": (42.7, 19.4), "Greece": (39.3, 22.0),
    "Moldova": (47.2, 28.5), "Turkey": (39.0, 34.5), "Syria": (35.0, 38.5), "Lebanon": (33.9, 35.9),
    "Israel": (31.5, 34.9), "Jordan": (31.2, 36.5), "Egypt": (30.3, 30.5), "Lybia": (31.0, 17.0),
    "Tunisia": (35.5, 9.5), "Algeria": (35.3, 3.0), "Morocco": (33.0, -6.0), "Crete": (35.2, 24.9),
    "Sardinia": (40.1, 9.0), "Cyprus": (35.0, 33.2), "Malta": (35.9, 14.4), "Italy": (44.0, 11.2),
    "Netherlands": (52.2, 5.5), "Denmark": (56.0, 9.5), "Belgium": (50.6, 4.6), "France": (46.6, 2.4),
    "Spain": (40.2, -3.6), "Portugal": (39.6, -8.0), "England": (52.5, -1.5), "Ireland": (53.2, -8.0),
    "Iceland": (64.9, -18.5), "Greenland": (68.0, -38.0), "Svalbard": (78.5, 16.0), "Norway": (60.8, 8.5),
    "Sweden": (63.0, 16.0), "Finland": (63.0, 26.5), "Switzerland": (46.8, 8.2), "Sami": (68.5, 24.0),
    "Sicily": (37.5, 14.0), "Scotland": (57.0, -4.2), "Wales": (52.3, -3.7), "Novaya Zemlya": (73.5, 55.0),
    "Crimea": (45.2, 34.3), "Palestine": (31.9, 35.3), "Armenia": (40.2, 45.0), "Azerbaijan": (40.3, 47.7),
    "Southern District (Russia)": (47.0, 41.0), "Central District (Russia)": (53.5, 36.0),
    "Volga District (Russia)": (54.5, 50.0), "Siberia": (67.0, 65.0), "Moscov (Russia)": (55.75, 37.6),
    "Georgia": (42.2, 43.5),
}
AMERICA = {
    "Haiti": (19.0, -72.4), "Dominican Republic": (18.9, -70.4), "Belize": (17.2, -88.6), "Yucatan": (20.5, -89.0),
    "Jamaica": (18.1, -77.3), "Cuba": (21.8, -79.5), "Florida": (28.0, -81.7), "Alabama": (32.8, -86.8),
    "South Carolina": (33.9, -80.9), "Bermuda": (32.3, -64.8), "Tennessee": (35.9, -86.4),
    "North Carolina": (35.5, -79.4), "Kentucky": (37.5, -85.3), "Virginia": (37.5, -78.8), "Delaware": (39.0, -75.5),
    "Maryland": (39.0, -76.8), "West Virginia": (38.6, -80.6), "Ohio": (40.3, -82.8), "Pennsylvania": (40.9, -77.8),
    "New York": (42.9, -75.5), "Vermont": (44.0, -72.7), "Michigan": (44.3, -85.4), "Maine": (45.3, -69.2),
    "Nova Scotia": (45.0, -63.0), "Quebec": (52.0, -71.5), "Ontario": (50.0, -85.0), "Newfoundland": (53.0, -58.0),
    "Nunavut": (64.0, -80.0), "West Greenland": (64.0, -49.0), "Avannaata": (68.0, -44.0), "Disko Bay": (70.0, -40.0),
    "National Park": (72.0, -33.0), "East Greenland": (69.0, -25.0),
}
GEORGIA_US = (32.7, -83.4)


def anchors(data):
    points, targets = [], []
    new_world = data["mapId"].lower() == "newworld"
    for country in data["countries"]:
        name = country["name"]
        if name == "Georgia" and new_world and country["x"] < -150:
            real = GEORGIA_US
        else:
            real = EUROPE.get(name) or AMERICA.get(name)
        if real is None:
            continue
        points.append((country["x"], country["z"]))
        targets.append((real[1], real[0]))  # lon, lat
    return np.array(points, float), np.array(targets, float)


class ThinPlate:
    """Affine plus regularised thin-plate spline, R^2 -> R^2."""

    def __init__(self, points, targets, smoothing):
        self.scale = 100.0
        p = points / self.scale
        n = len(p)
        k = self.kernel(p[:, None, :] - p[None, :, :]) + smoothing * np.eye(n)
        poly = np.hstack([np.ones((n, 1)), p])
        system = np.zeros((n + 3, n + 3))
        system[:n, :n], system[:n, n:], system[n:, :n] = k, poly, poly.T
        rhs = np.zeros((n + 3, 2))
        rhs[:n] = targets
        solution = np.linalg.solve(system, rhs)
        self.points, self.weights, self.affine = p, solution[:n], solution[n:]

    @staticmethod
    def kernel(delta):
        r2 = np.sum(delta * delta, axis=-1)
        with np.errstate(divide="ignore", invalid="ignore"):
            value = .5 * r2 * np.log(r2)
        return np.nan_to_num(value)

    def __call__(self, xy):
        q = xy / self.scale
        k = self.kernel(q[:, None, :] - self.points[None, :, :])
        return k @ self.weights + self.affine[0] + q @ self.affine[1:]


def gaussian(image, sigma):
    radius = int(3 * sigma + .5)
    x = np.arange(-radius, radius + 1)
    kernel = np.exp(-.5 * (x / sigma) ** 2)
    kernel /= kernel.sum()
    out = image
    for axis in (0, 1):
        pad = [(0, 0)] * out.ndim
        pad[axis] = (radius, radius)
        padded = np.pad(out, pad, mode="edge")
        acc = np.zeros_like(out)
        for i, w in enumerate(kernel):
            acc += w * np.take(padded, np.arange(i, i + out.shape[axis]), axis=axis)
        out = acc
    return out


def satellite():
    months = [np.asarray(Image.open(path).convert("RGB"), np.float32) / 255 for path in SOURCES]
    land = np.ones(months[0].shape[:2], np.float32)
    for image in months:
        r, g, b = image[..., 0], image[..., 1], image[..., 2]
        # Blue Marble NG ocean and lakes are dark navy; land (incl. ice) is not blue-dominant.
        navy = (b > r + .035) & (b > g) & ((r + g + b) < 1.1)
        shallows = (b > r + .1) & (g > r + .05)  # turquoise banks (Bahamas, lagoons)
        black = (r + g + b) < .12                # unlit coastal sea
        land *= ~(navy | shallows | black)
    image = sum(months) / len(months)
    # Normalised convolution: colour is averaged over land pixels only, so a
    # stylised coastline never picks up sea colour. Wider kernels fill map land
    # that falls on real sea (small islands, straits) from the nearest real land.
    result, total = None, None
    for sigma in (1.5, 8.0, 40.0):
        weight = gaussian(land, sigma)
        colour = gaussian(image * land[..., None], sigma) / np.maximum(weight[..., None], 1e-20)
        confidence = np.clip(weight / .2, 0, 1)[..., None] if sigma < 40 else np.ones_like(weight)[..., None]
        if result is None:
            result, total = colour * confidence, confidence
        else:
            use = (1 - total)
            result, total = result + colour * use * confidence, total + use * confidence
    # Confidence 0 means no real land within ~8 degrees (e.g. stylised Bermuda).
    known = gaussian(land, 40.0) > 1e-4
    return result / np.maximum(total, 1e-6), known.astype(np.float32)


def inpaint(rgb, known):
    """Fill unknown texels by diffusion from known neighbours (map space)."""
    rgb, known = rgb.copy(), known.copy()
    for _ in range(400):
        if known.all():
            break
        acc = np.zeros_like(rgb); cnt = np.zeros(known.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            shifted = np.roll(np.roll(known, dy, 0), dx, 1)
            acc += np.roll(np.roll(rgb, dy, 0), dx, 1) * shifted[..., None]; cnt += shifted
        grow = (~known) & (cnt > 0)
        rgb[grow] = acc[grow] / cnt[grow][:, None]; known |= grow
    return rgb


def sample(image, lon, lat):
    h, w = image.shape[:2]
    x = np.clip((lon + 180) / 360 * w - .5, 0, w - 1.001)
    y = np.clip((90 - lat) / 180 * h - .5, 0, h - 1.001)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = (x - x0)[..., None], (y - y0)[..., None]
    a, b = image[y0, x0], image[y0, x0 + 1]
    c, d = image[y0 + 1, x0], image[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def aridity(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    warmth = np.clip((r - g + .03) / .16, 0, 1)
    bright = np.clip(((r + g + b) / 3 - .18) / .30, 0, 1)
    snow = np.clip((np.minimum(np.minimum(r, g), b) - .55) / .2, 0, 1)
    return np.clip(warmth * (.35 + .65 * bright), 0, 1) * (1 - snow)


def lift_greens(rgb):
    """Brighter, slightly more saturated vegetation; ochre, sand and ice stay as imaged."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    green = np.clip((g - np.maximum(r, b) + .01) / .06, 0, 1)[..., None]
    luma = (rgb * [.30, .59, .11]).sum(-1, keepdims=True)
    lifted = (luma + (rgb - luma) * 1.25) * 1.22
    return rgb + (np.clip(lifted, 0, 1) - rgb) * green


def bake(name, colour, weight, preview):
    data = json.loads((MAPS / f"{name}.json").read_text(encoding="utf-8"))
    points, targets = anchors(data)
    warp = ThinPlate(points, targets, smoothing=.4)
    residual = np.abs(warp(points) - targets).max(axis=0)
    min_x, max_x = data["playableMinX"], data["playableMaxX"]
    min_z, max_z = data["playableMinZ"], data["playableMaxZ"]
    width = int(round((max_x - min_x) / TEXEL_METRES)) + 1
    height = int(round((max_z - min_z) / TEXEL_METRES)) + 1
    xs = min_x + np.arange(width) * TEXEL_METRES
    zs = min_z + np.arange(height) * TEXEL_METRES
    grid = np.stack(np.meshgrid(xs, zs), -1).reshape(-1, 2)
    lonlat = warp(grid)
    # Fold check: the warp must keep orientation everywhere on the map.
    lon = lonlat[:, 0].reshape(height, width)
    lat = lonlat[:, 1].reshape(height, width)
    folds = int(np.sum((np.diff(lon, axis=1)[:-1] * np.diff(lat, axis=0)[:, :-1]
                        - np.diff(lat, axis=1)[:-1] * np.diff(lon, axis=0)[:, :-1]) <= 0))
    rgb = inpaint(sample(colour, lon, lat), sample(weight[..., None], lon, lat)[..., 0] > .5)
    # Light smoothing in map space keeps borders organic at tactical zoom.
    rgb = gaussian(rgb.astype(np.float32), 1.2)
    arid = aridity(rgb)
    rgb = lift_greens(rgb)
    out = np.dstack([np.clip(rgb, 0, 1) * 255, arid[..., None] * 255]).round().astype(np.uint8)
    # Row 0 is the playable minimum Z (texture origin bottom-left in Unity).
    image = Image.fromarray(out[::-1], "RGBA")
    target = MAPS / f"{name}Ground.bytes"
    image.save(target, format="PNG", optimize=True)
    print(f"{name}: {width}x{height} anchors={len(points)} residual lon/lat={residual.round(2)} folds={folds} -> {target.name} {target.stat().st_size} B")
    if preview:
        land = np.array(data["landSamples"]).reshape(data["height"], data["width"])
        ix = np.clip(np.round((xs - data["originX"]) / data["cellSize"]).astype(int), 0, data["width"] - 1)
        iz = np.clip(np.round((zs - data["originZ"]) / data["cellSize"]).astype(int), 0, data["height"] - 1)
        mask = land[np.ix_(iz, ix)] > 0
        view = out[..., :3].copy()
        view[~mask] = (20, 40, 90)
        Image.fromarray(view[::-1]).resize((width * 3, height * 3), Image.NEAREST).save(Path(preview) / f"{name}-ground-preview.png")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview")
    args = parser.parse_args()
    for source in SOURCES:
        if not source.exists():
            raise SystemExit(f"Missing {source.relative_to(ROOT)}; download it from the URL in this script's docstring.")
    colour, weight = satellite()
    for name in ("Europe", "NewWorld"):
        bake(name, colour, weight, args.preview)


if __name__ == "__main__":
    main()
