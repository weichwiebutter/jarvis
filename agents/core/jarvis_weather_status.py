#!/usr/bin/env python3
"""
Jarvis Weather Status

Builds a defensive read-only weather status for the Jarvis Home Dashboard.
Open-Meteo is optional and does not require API keys. Network/API failures are
reported as warnings instead of raising.
"""

from __future__ import annotations

import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))


OPEN_METEO_PROVIDER = "open-meteo"
OPEN_METEO_FORECAST_URL = "https://api.open-meteo.com/v1/forecast"
REQUEST_TIMEOUT_SECONDS = 4

KNOWN_LOCATIONS = {
    "frankfurt,de": {
        "label": "Frankfurt,DE",
        "latitude": 50.1109,
        "longitude": 8.6821,
    },
    "frankfurt am main,de": {
        "label": "Frankfurt,DE",
        "latitude": 50.1109,
        "longitude": 8.6821,
    },
    "berlin,de": {
        "label": "Berlin,DE",
        "latitude": 52.52,
        "longitude": 13.405,
    },
    "hamburg,de": {
        "label": "Hamburg,DE",
        "latitude": 53.5511,
        "longitude": 9.9937,
    },
    "munich,de": {
        "label": "Munich,DE",
        "latitude": 48.1351,
        "longitude": 11.582,
    },
    "muenchen,de": {
        "label": "Munich,DE",
        "latitude": 48.1351,
        "longitude": 11.582,
    },
}

WEATHER_CODES = {
    0: "clear",
    1: "mainly_clear",
    2: "partly_cloudy",
    3: "overcast",
    45: "fog",
    48: "depositing_rime_fog",
    51: "light_drizzle",
    53: "moderate_drizzle",
    55: "dense_drizzle",
    56: "light_freezing_drizzle",
    57: "dense_freezing_drizzle",
    61: "slight_rain",
    63: "moderate_rain",
    65: "heavy_rain",
    66: "light_freezing_rain",
    67: "heavy_freezing_rain",
    71: "slight_snow",
    73: "moderate_snow",
    75: "heavy_snow",
    77: "snow_grains",
    80: "slight_rain_showers",
    81: "moderate_rain_showers",
    82: "violent_rain_showers",
    85: "slight_snow_showers",
    86: "heavy_snow_showers",
    95: "thunderstorm",
    96: "thunderstorm_with_slight_hail",
    99: "thunderstorm_with_heavy_hail",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _empty_weather_status(
    location: str,
    status: str,
    api_called: bool,
    warnings: list[str],
) -> dict[str, Any]:
    return {
        "generated_at": utc_now(),
        "status": status,
        "location": location,
        "provider": OPEN_METEO_PROVIDER,
        "temperature": None,
        "condition": None,
        "wind": None,
        "api_called": api_called,
        "warnings": warnings,
        "read_only": True,
    }


def _normalize_location(location: str) -> str:
    return " ".join(location.strip().lower().split())


def _coordinates_for_location(location: str) -> dict[str, Any] | None:
    return KNOWN_LOCATIONS.get(_normalize_location(location))


def _build_open_meteo_url(latitude: float, longitude: float) -> str:
    query = urllib.parse.urlencode(
        {
            "latitude": latitude,
            "longitude": longitude,
            "current": ",".join(
                [
                    "temperature_2m",
                    "weather_code",
                    "wind_speed_10m",
                    "wind_direction_10m",
                ]
            ),
            "timezone": "auto",
        }
    )
    return f"{OPEN_METEO_FORECAST_URL}?{query}"


def _fetch_open_meteo_current_weather(latitude: float, longitude: float) -> dict[str, Any]:
    request = urllib.request.Request(
        _build_open_meteo_url(latitude, longitude),
        headers={
            "User-Agent": "JarvisHomeDashboard/1.0 read-only weather status",
        },
    )

    with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT_SECONDS) as response:
        payload = response.read().decode("utf-8")

    data = json.loads(payload)
    if not isinstance(data, dict):
        raise ValueError("Open-Meteo returned non-dict data.")

    current = data.get("current")
    current_units = data.get("current_units")
    if not isinstance(current, dict):
        raise ValueError("Open-Meteo response did not include current weather data.")
    if not isinstance(current_units, dict):
        current_units = {}

    return {
        "current": current,
        "current_units": current_units,
    }


def _condition_from_code(code: Any) -> dict[str, Any]:
    if isinstance(code, bool):
        normalized_code = None
    elif isinstance(code, int):
        normalized_code = code
    elif isinstance(code, float) and code.is_integer():
        normalized_code = int(code)
    else:
        normalized_code = None

    return {
        "code": normalized_code,
        "text": WEATHER_CODES.get(normalized_code, "unknown"),
    }


def build_weather_status(location: str = "Frankfurt,DE") -> dict[str, Any]:
    requested_location = (location or "Frankfurt,DE").strip() or "Frankfurt,DE"
    warnings: list[str] = []
    coordinates = _coordinates_for_location(requested_location)

    if coordinates is None:
        warnings.append(
            "Location is not in the local read-only weather coordinate map; "
            "Open-Meteo request was not attempted."
        )
        return _empty_weather_status(
            location=requested_location,
            status="planned",
            api_called=False,
            warnings=warnings,
        )

    try:
        weather = _fetch_open_meteo_current_weather(
            latitude=float(coordinates["latitude"]),
            longitude=float(coordinates["longitude"]),
        )
    except urllib.error.URLError as exc:
        warnings.append(f"Open-Meteo unavailable: {exc}")
        return _empty_weather_status(
            location=str(coordinates["label"]),
            status="unavailable",
            api_called=True,
            warnings=warnings,
        )
    except TimeoutError as exc:
        warnings.append(f"Open-Meteo request timed out: {exc}")
        return _empty_weather_status(
            location=str(coordinates["label"]),
            status="unavailable",
            api_called=True,
            warnings=warnings,
        )
    except Exception as exc:
        warnings.append(f"Open-Meteo weather status failed: {exc}")
        return _empty_weather_status(
            location=str(coordinates["label"]),
            status="unavailable",
            api_called=True,
            warnings=warnings,
        )

    current = weather["current"]
    current_units = weather["current_units"]
    temperature_value = current.get("temperature_2m")
    wind_speed = current.get("wind_speed_10m")
    wind_direction = current.get("wind_direction_10m")

    return {
        "generated_at": utc_now(),
        "status": "available",
        "location": str(coordinates["label"]),
        "provider": OPEN_METEO_PROVIDER,
        "temperature": {
            "value": temperature_value,
            "unit": current_units.get("temperature_2m", "°C"),
        },
        "condition": _condition_from_code(current.get("weather_code")),
        "wind": {
            "speed": wind_speed,
            "speed_unit": current_units.get("wind_speed_10m", "km/h"),
            "direction": wind_direction,
            "direction_unit": current_units.get("wind_direction_10m", "°"),
        },
        "api_called": True,
        "warnings": warnings,
        "read_only": True,
    }


def main() -> int:
    location = " ".join(sys.argv[1:]).strip() or "Frankfurt,DE"
    print(json.dumps(build_weather_status(location), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
