"""Build and run the Java JMH serializer benchmark from pinned Maven Central artifacts."""

import argparse
from concurrent.futures import ThreadPoolExecutor
from http.client import IncompleteRead
import os
from pathlib import Path
import shutil
import subprocess
from tempfile import TemporaryDirectory
from time import sleep
from urllib.error import HTTPError, URLError
from urllib.request import urlopen


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
CENTRAL = "https://repo.maven.apache.org/maven2"
DEPENDENCIES = (
    ("org.openjdk.jmh", "jmh-core", "1.37"),
    ("org.openjdk.jmh", "jmh-generator-annprocess", "1.37"),
    ("net.sf.jopt-simple", "jopt-simple", "5.0.4"),
    ("org.apache.commons", "commons-math3", "3.6.1"),
    ("com.esotericsoftware", "kryo", "5.6.2"),
    ("com.esotericsoftware", "reflectasm", "1.11.9"),
    ("com.esotericsoftware", "minlog", "1.3.1"),
    ("org.objenesis", "objenesis", "3.4"),
    ("com.fasterxml.jackson.core", "jackson-annotations", "2.20"),
    ("com.fasterxml.jackson.core", "jackson-core", "2.20.2"),
    ("com.fasterxml.jackson.core", "jackson-databind", "2.20.2"),
    ("com.fasterxml.jackson.dataformat", "jackson-dataformat-cbor", "2.20.2"),
    ("org.msgpack", "msgpack-core", "0.9.11"),
    ("org.msgpack", "jackson-dataformat-msgpack", "0.9.11"),
    ("com.google.code.gson", "gson", "2.15.0"),
    ("com.alibaba.fastjson2", "fastjson2", "2.0.66"),
)


def artifact(work, coordinates):
    group, name, version = coordinates
    jar = f"{name}-{version}.jar"
    destination = work / jar
    if not destination.exists():
        url = f"{CENTRAL}/{group.replace('.', '/')}/{name}/{version}/{jar}"
        with TemporaryDirectory(dir=work, prefix="download-") as staging:
            temporary = Path(staging) / jar
            for attempt in range(3):
                try:
                    with urlopen(url, timeout=30) as source, temporary.open("wb") as handle:
                        shutil.copyfileobj(source, handle)
                        length = source.headers.get("Content-Length")
                        if length is not None and handle.tell() != int(length):
                            raise IncompleteRead(b"", int(length) - handle.tell())
                    break
                except (HTTPError, URLError, IncompleteRead, TimeoutError, ConnectionError) as error:
                    permanent_http_error = isinstance(error, HTTPError) and error.code < 500 and error.code != 429
                    if permanent_http_error or attempt == 2:
                        raise RuntimeError(f"Could not download {jar} from Maven Central.") from error
                    sleep(attempt + 1)
            temporary.replace(destination)
    return str(destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("models", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--work-dir", type=Path, default=ROOT / ".tmp/java-benchmark")
    parser.add_argument("--quick", action="store_true", help="Use one fork and shorter iterations for a local smoke run.")
    parser.add_argument("--verify-only", action="store_true", help="Validate all adapters and fixtures without collecting timings.")
    args = parser.parse_args()
    work = args.work_dir.resolve()
    jars = work / "jars"
    jars.mkdir(parents=True, exist_ok=True)
    with ThreadPoolExecutor(max_workers=4) as pool:
        libraries = list(pool.map(lambda item: artifact(jars, item), DEPENDENCIES))
    classpath = os.pathsep.join(libraries)
    native_source = ROOT / "src/LuminPack.Java/src/main/java"
    sources = [*sorted(native_source.rglob("*.java")), *sorted((HERE / "src").rglob("*.java"))]
    options = [name for enabled, name in ((args.quick, "quick"), (args.verify_only, "verify")) if enabled]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=work, prefix="classes-") as classes:
        subprocess.run(["javac", "--release", "17", "-encoding", "UTF-8", "-cp", classpath,
                        "-processorpath", classpath, "-processor", "org.openjdk.jmh.generators.BenchmarkProcessor",
                        "-d", classes, *sources], check=True)
        subprocess.run(["java", "-cp", os.pathsep.join([classes, classpath]), "luminpack.benchmarks.Main",
                        args.models.resolve(), args.output.resolve(), *options], check=True)


if __name__ == "__main__":
    main()
