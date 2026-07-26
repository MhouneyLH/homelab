import os

Import("env")  # noqa: F821

REQUIRED_KEYS = (
    "INIT_WIFI_SSID",
    "INIT_WIFI_PASSWORD",
    "INIT_MQTT_BROKER_HOST",
    "INIT_MQTT_BROKER_PORT",
    "INIT_PLANT_ID",
)

GENERATED_HEADER = "generated_config.h"


def load_dotenv(path):
    if not os.path.isfile(path):
        return
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, _, value = line.partition("=")
            key = key.strip()
            value = value.strip().strip('"').strip("'")
            os.environ.setdefault(key, value)


def c_string_literal(value):
    escaped = value.replace("\\", "\\\\").replace('"', '\\"')
    return '"{}"'.format(escaped)


project_dir = env.subst("$PROJECT_DIR")  # noqa: F821
load_dotenv(os.path.join(project_dir, ".env"))

missing = [key for key in REQUIRED_KEYS if not os.environ.get(key)]
if missing:
    raise SystemExit(
        "Missing required .env keys: "
        + ", ".join(missing)
        + " (copy .env.example to .env and fill in real values)"
    )

# Written to a generated header (gitignored) instead of passed as -D flags,
# so arbitrary characters in secrets (spaces, quotes, &, ...) never have to
# survive argv/shell tokenizing.
header_path = os.path.join(project_dir, "include", GENERATED_HEADER)
with open(header_path, "w", encoding="utf-8") as f:
    f.write("#pragma once\n\n")
    f.write("#define WIFI_SSID {}\n".format(c_string_literal(os.environ["INIT_WIFI_SSID"])))
    f.write("#define WIFI_PASSWORD {}\n".format(c_string_literal(os.environ["INIT_WIFI_PASSWORD"])))
    f.write("#define MQTT_BROKER_HOST {}\n".format(c_string_literal(os.environ["INIT_MQTT_BROKER_HOST"])))
    f.write("#define MQTT_BROKER_PORT {}\n".format(int(os.environ["INIT_MQTT_BROKER_PORT"])))
    f.write("#define PLANT_ID {}\n".format(c_string_literal(os.environ["INIT_PLANT_ID"])))
