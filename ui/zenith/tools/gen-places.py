"""Builds src/data/places.txt, the offline nearest-town table for the Home page Location.

Source: GeoNames (https://www.geonames.org/), CC BY 4.0. Download cities1000.zip and
admin1CodesASCII.txt from https://download.geonames.org/export/dump/ into a separate folder, then:

    python -I tools/gen-places.py <cities1000.zip> <admin1CodesASCII.txt> src/data/places.txt

Output format (UTF-8, LF):
    line 1:  "# GeoNames cities1000 ..." attribution
    "@" lines: region labels, referenced by index ("MO" for US states, the region name elsewhere)
    data lines: name<TAB>region index<TAB>latitude*100<TAB>longitude*100
"""
import io
import sys
import zipfile

# Sections of a populated place, historical, abandoned or destroyed places are not useful labels.
SKIP_FEATURES = {"PPLX", "PPLH", "PPLQ", "PPLW", "PPLCH"}


def main(cities_zip: str, admin_file: str, out_path: str) -> None:
    admin_names = {}
    with open(admin_file, encoding="utf-8") as f:
        for line in f:
            parts = line.rstrip("\n").split("\t")
            if len(parts) >= 2:
                admin_names[parts[0]] = parts[1]

    labels: list[str] = []
    label_index: dict[str, int] = {}
    rows: list[tuple[str, int, int, int]] = []
    seen: set[tuple[str, int, int]] = set()

    with zipfile.ZipFile(cities_zip) as z:
        with z.open("cities1000.txt") as raw:
            for line in io.TextIOWrapper(raw, encoding="utf-8"):
                p = line.rstrip("\n").split("\t")
                if len(p) < 15 or p[7] in SKIP_FEATURES:
                    continue
                name = p[1].strip()
                if not name or "\t" in name:
                    continue
                country, admin1 = p[8], p[10]
                if country == "US" and len(admin1) == 2 and admin1.isalpha():
                    label = admin1  # "Imperial, MO"
                else:
                    label = admin_names.get(f"{country}.{admin1}") or country
                lat = round(float(p[4]) * 100)
                lon = round(float(p[5]) * 100)
                key = (name, lat, lon)
                if key in seen:
                    continue
                seen.add(key)
                if label not in label_index:
                    label_index[label] = len(labels)
                    labels.append(label)
                rows.append((name, label_index[label], lat, lon))

    with open(out_path, "w", encoding="utf-8", newline="\n") as out:
        out.write("# GeoNames cities1000 (https://www.geonames.org/), CC BY 4.0\n")
        for label in labels:
            out.write("@" + label + "\n")
        for name, idx, lat, lon in rows:
            out.write(f"{name}\t{idx}\t{lat}\t{lon}\n")
    print(f"{len(rows)} places, {len(labels)} regions -> {out_path}")


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2], sys.argv[3])
