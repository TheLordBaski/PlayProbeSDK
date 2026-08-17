#!/usr/bin/env python3
"""Renders the HTML manual to the PDF that ships inside the package.

    python3 Assets/PlayProbeSDK/Tools~/export_docs_pdf.py

Writes Documentation/PlayProbe-Unity-SDK.pdf next to the HTML. Re-running overwrites it.

Add --publish to push the same files to the website in the same run, so the package, the PDF and
playprobe.io/docs/unity-sdk can never disagree:

    python3 Assets/PlayProbeSDK/Tools~/export_docs_pdf.py \
        --publish ~/dev/playprobe.io/public/docs

WHY NOT pdfkit
--------------
pdfkit is a thin wrapper around wkhtmltopdf, whose rendering engine is the Qt fork of WebKit from
around 2012. It does not implement CSS custom properties, and this stylesheet defines every single
colour as one (`--bg`, `--surface`, `--primary`, ...). Under wkhtmltopdf all of them resolve to
nothing, so the manual comes out as unstyled black text on white with no panels, no code shading and
no layout. It also has no CSS grid, which the page uses for its two-column shell.

So this drives headless Chrome instead, through exactly the same code path as File > Print > Save as
PDF. That is the target the print stylesheet in the HTML was written against, which is why the
output matches what you see when you print the page by hand.

Chrome is the only requirement. Everything else is the standard library.
"""

import argparse
import html.parser
import os
import re
import shutil
import subprocess
import sys
import tempfile
import urllib.parse

# Ordered by how likely each is to be the one present. The WSL entries matter because this repo
# usually lives on a Windows drive mounted into Linux, where the Windows browser is often the only
# one installed.
CHROME_CANDIDATES = [
    "google-chrome",
    "google-chrome-stable",
    "chromium",
    "chromium-browser",
    "microsoft-edge",
    "/mnt/c/Program Files/Google/Chrome/Application/chrome.exe",
    "/mnt/c/Program Files (x86)/Google/Chrome/Application/chrome.exe",
    "/mnt/c/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
]

REPO_TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
PACKAGE_DIR = os.path.dirname(REPO_TOOLS_DIR)
DEFAULT_HTML = os.path.join(PACKAGE_DIR, "Documentation", "playprobe-unity-sdk.html")
DEFAULT_PDF = os.path.join(PACKAGE_DIR, "Documentation", "PlayProbe-Unity-SDK.pdf")


class ImageCollector(html.parser.HTMLParser):
    """Collects img/@src, ignoring anything inside a comment.

    The placeholder comments left in the HTML still name the originally suggested filenames, and
    several of those were never used — a plain regex over the file reports them as missing images
    that are not actually missing.
    """

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.sources = []

    def handle_starttag(self, tag, attrs):
        if tag != "img":
            return

        src = dict(attrs).get("src", "")
        if src and not src.startswith("data:"):
            self.sources.append(src)


def find_chrome(explicit):
    if explicit:
        if os.path.exists(explicit) or shutil.which(explicit):
            return explicit
        sys.exit(f"error: no browser at {explicit}")

    for candidate in CHROME_CANDIDATES:
        resolved = shutil.which(candidate) if "/" not in candidate else (
            candidate if os.path.exists(candidate) else None
        )
        if resolved:
            return resolved

    sys.exit(
        "error: no Chrome, Chromium or Edge found.\n"
        "Install one, or pass --chrome /path/to/chrome."
    )


def check_images(html_path):
    """Fails before rendering if a referenced image is missing.

    Chrome renders a missing image as an empty box and still exits 0, so a typo'd filename would
    otherwise reach the PDF as a silent hole in the page.
    """
    with open(html_path, encoding="utf-8") as handle:
        collector = ImageCollector()
        collector.feed(handle.read())

    base = os.path.dirname(html_path)
    missing = []

    for src in collector.sources:
        if urllib.parse.urlparse(src).scheme in ("http", "https"):
            print(f"  ! remote image, will not render offline: {src}")
            continue

        path = os.path.join(base, urllib.parse.unquote(src))
        if not os.path.isfile(path):
            missing.append(src)

    if missing:
        sys.exit(
            "error: these images are referenced but not on disk:\n  "
            + "\n  ".join(missing)
        )

    return len(collector.sources)


def page_count(pdf_path):
    """Best-effort page count from the PDF's page tree. Returns None if it cannot tell."""
    with open(pdf_path, "rb") as handle:
        blob = handle.read()

    counts = [int(match) for match in re.findall(rb"/Type\s*/Pages.{0,200}?/Count\s+(\d+)", blob, re.S)]
    if counts:
        return max(counts)

    pages = len(re.findall(rb"/Type\s*/Page[^s]", blob))
    return pages or None


def export(html_path, pdf_path, chrome):
    # Chrome writes the PDF itself, so it needs an absolute file:// URL rather than a relative path.
    url = "file://" + urllib.parse.quote(os.path.abspath(html_path))

    # A Windows Chrome reached through WSL cannot write to a Linux path, and cannot read one either.
    # Detect that and bail with something more useful than Chrome's silence.
    if chrome.endswith(".exe") and not os.path.abspath(html_path).startswith("/mnt/"):
        sys.exit(
            "error: the Windows browser cannot reach a Linux-only path.\n"
            f"       {html_path}\n"
            "       Install Chrome inside WSL, or move the docs onto a mounted drive."
        )

    with tempfile.TemporaryDirectory(prefix="playprobe-pdf-") as profile:
        command = [
            chrome,
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            f"--user-data-dir={profile}",
            # Chrome exits as soon as layout settles; the budget is an upper bound, not a sleep.
            "--virtual-time-budget=20000",
            "--run-all-compositor-stages-before-draw",
            # The margins, page size and colours all come from the stylesheet's @page and
            # @media print rules. Adding Chrome's own header/footer would stamp a URL and a date
            # over them.
            "--no-pdf-header-footer",
            f"--print-to-pdf={os.path.abspath(pdf_path)}",
            url,
        ]

        result = subprocess.run(command, capture_output=True, text=True, timeout=180)

    # Chrome is noisy on stderr even when it succeeds, so the exit code and the file are what count.
    if result.returncode != 0:
        sys.stderr.write(result.stderr)
        sys.exit(f"error: chrome exited {result.returncode}")


def publish(html_path, pdf_path, target):
    """Copies the manual, its images and the PDF into the website's public folder.

    The site frames this exact file rather than keeping a JSX copy, so publishing is a file copy
    and there is never a second version of the text to keep in step.
    """
    target = os.path.abspath(target)
    os.makedirs(os.path.join(target, "images"), exist_ok=True)

    # The site serves it under a shorter name than the package uses.
    shutil.copy2(html_path, os.path.join(target, "unity-sdk.html"))
    shutil.copy2(pdf_path, os.path.join(target, os.path.basename(pdf_path)))

    source_images = os.path.join(os.path.dirname(html_path), "images")
    copied = 0

    if os.path.isdir(source_images):
        for name in sorted(os.listdir(source_images)):
            # .meta files are Unity's, and mean nothing to a web server.
            if name.endswith(".meta") or not os.path.isfile(os.path.join(source_images, name)):
                continue

            shutil.copy2(os.path.join(source_images, name), os.path.join(target, "images", name))
            copied += 1

    print(f"publish : {target}")
    print(f"          unity-sdk.html, {os.path.basename(pdf_path)}, {copied} images")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--html", default=DEFAULT_HTML, help="source HTML (default: the packaged manual)")
    parser.add_argument("--pdf", default=DEFAULT_PDF, help="output PDF")
    parser.add_argument("--chrome", default=None, help="browser binary to use")
    parser.add_argument(
        "--publish",
        default=None,
        metavar="DIR",
        help="also copy the HTML, images and PDF here (the site's public/docs)",
    )
    args = parser.parse_args()

    if not os.path.isfile(args.html):
        sys.exit(f"error: no HTML at {args.html}")

    chrome = find_chrome(args.chrome)
    print(f"browser : {chrome}")
    print(f"source  : {args.html}")

    image_count = check_images(args.html)
    print(f"images  : {image_count} referenced, all present")

    export(args.html, args.pdf, chrome)

    if not os.path.isfile(args.pdf):
        sys.exit("error: chrome reported success but wrote no file")

    with open(args.pdf, "rb") as handle:
        if handle.read(5) != b"%PDF-":
            sys.exit("error: output is not a PDF")

    size = os.path.getsize(args.pdf)
    pages = page_count(args.pdf)

    print(f"output  : {args.pdf}")
    print(f"          {size / 1024:.0f} KB" + (f", {pages} pages" if pages else ""))

    if args.publish:
        publish(args.html, args.pdf, args.publish)


if __name__ == "__main__":
    main()
