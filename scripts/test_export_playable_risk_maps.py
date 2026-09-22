"""Focused regression checks for authored W3E/WPM navigation export."""
from __future__ import annotations

import unittest
import base64

try:
    from .export_playable_risk_maps import MAPS, export, node_index, w3e, wpm
except ImportError:  # direct `python scripts/test_export_playable_risk_maps.py`
    from export_playable_risk_maps import MAPS, export, node_index, w3e, wpm


class AuthoredNavigationTests(unittest.TestCase):
    def test_wpm_headers_and_dimensions(self) -> None:
        self.assertEqual((wpm(MAPS[0]["source_dir"] / "war3map.wpm")["width"],
                          wpm(MAPS[0]["source_dir"] / "war3map.wpm")["height"]), (1024, 1024))
        self.assertEqual((wpm(MAPS[1]["source_dir"] / "war3map.wpm")["width"],
                          wpm(MAPS[1]["source_dir"] / "war3map.wpm")["height"]), (1536, 1536))

    def test_port_anchor_retains_raw_w3e_and_wpm_evidence(self) -> None:
        payload = export(MAPS[0])
        city = payload["cities"][25]  # authored city index 26, h00O Estonia
        self.assertEqual(city["id"], "europe-026")
        self.assertEqual(city["sourceRawId"], "h00O")
        self.assertEqual((city["sourceNativeX"], city["sourceNativeY"]), (4128.0, 4640.0))
        self.assertEqual((city["claimNativeX"], city["claimNativeY"]), (4032.0, 4352.0))
        authored = city["sourceNavigation"]["city"]
        self.assertFalse(authored["w3e"]["landByW3eRule"])
        self.assertEqual(authored["w3e"]["terrainFlags"], 71)
        self.assertAlmostEqual(authored["w3e"]["waterDepthNative"], 26.9, places=5)
        self.assertEqual(authored["wpm"], {"x": 561, "y": 657, "rawByte": 8})
        self.assertEqual(city["sourceNavigation"]["semantics"],
                         "w3e-surface-rule-plus-raw-wpm; blocking-bit-correlation-needs-engine-validation")

    def test_full_pathing_grid_is_compact_raw_bytes(self) -> None:
        payload = export(MAPS[0])
        self.assertEqual(payload["pathingCellSize"], 0.64)
        raw = base64.b64decode(payload["pathingSamples"])
        self.assertEqual(len(raw), payload["pathingWidth"] * payload["pathingHeight"])
        self.assertEqual(raw[657 * payload["pathingWidth"] + 561], 8)
        self.assertEqual(payload["metadata"]["pathing"]["bitSemantics"]["0x02"], "blocksGround")
        self.assertEqual(payload["metadata"]["pathing"]["bitSemantics"]["0x40"], "blocksBoats")

    def test_empirical_blocking_bits_match_authored_anchors(self) -> None:
        for spec in MAPS:
            payload = export(spec)
            ports = [city for city in payload["cities"] if city["port"]]
            self.assertTrue(all(city["sourceNavigation"]["claim"]["wpm"]["rawByte"] == 0x08
                                for city in ports))
            land_sources = [city for city in payload["cities"] if city["sourceRawId"] != "h00O"]
            self.assertTrue(all(city["sourceNavigation"]["city"]["wpm"]["rawByte"] & 0x40
                                for city in land_sources))

    def test_source_type_counts_and_city_counts_are_unchanged(self) -> None:
        europe, world = (export(spec) for spec in MAPS)
        self.assertEqual(len(europe["cities"]), 212)
        self.assertEqual(len(world["cities"]), 293)
        self.assertEqual({key: sum(city["sourceRawId"] == key for city in europe["cities"])
                          for key in {"h00N", "h00O"}}, {"h00N": 168, "h00O": 44})
        self.assertEqual({key: sum(city["sourceRawId"] == key for city in world["cities"])
                          for key in {"h00N", "h00O", "h00T"}},
                         {"h00N": 173, "h00O": 59, "h00T": 61})
        self.assertTrue(all("sourceNativeX" in country and "sourceNavigation" in country
                            for country in europe["countries"]))
        self.assertTrue(all("sourceNativeX" in country and "sourceNavigation" in country
                            for country in world["countries"]))

    def test_w3e_water_flag_is_not_pathing_semantics(self) -> None:
        terrain = w3e(MAPS[0]["source_dir"] / "war3map.w3e")
        index = node_index(4128.0, 4640.0, terrain)
        self.assertIsNotNone(index)
        self.assertEqual(terrain["terrain_flags_values"][index] & 0x40, 0x40)
        self.assertIn("semantics", wpm(MAPS[0]["source_dir"] / "war3map.wpm")["source"])
        self.assertEqual(wpm(MAPS[0]["source_dir"] / "war3map.wpm")["source"]["semantics"],
                         "raw-bytes-with-blocking-bit-correlation")


if __name__ == "__main__":
    unittest.main()
