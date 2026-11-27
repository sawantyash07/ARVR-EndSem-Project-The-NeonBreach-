"""Build the NEON BREACH ESE report: assemble src/part*.html, number chapters/sections/figures/tables/listings,
pull code listings straight from the project's source files, render to PDF with Microsoft Edge (headless Chromium),
then fill the table of contents and lists with the real page numbers and render again."""
import glob, html, os, re, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPORT = os.path.dirname(HERE)                       # Docs/ESE_Report
PROJECT = os.path.dirname(os.path.dirname(REPORT))   # project root
BUILD = os.path.join(REPORT, "build")
OUT_PDF = os.path.join(os.path.dirname(REPORT), "NEON_BREACH_Final_ESE_Project_Report.pdf")
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
os.makedirs(BUILD, exist_ok=True)

src = "\n".join(open(f, encoding="utf-8").read() for f in sorted(glob.glob(os.path.join(HERE, "part*.html"))))

# ------------------------------------------------------------------ code listings from real files
def code_file(m):
    attrs = dict(re.findall(r'(\w+)="([^"]*)"', m.group(1)))
    path = os.path.join(PROJECT, attrs["src"].replace("/", os.sep))
    lines = open(path, encoding="utf-8").read().splitlines()
    out = []
    for rng in attrs["lines"].split(","):
        a, b = (int(x) for x in rng.split("-"))
        if out:
            out.append("    // ...")
        chunk = lines[a - 1:b]
        out.extend(chunk)
    # remove common indentation
    ind = min((len(l) - len(l.lstrip()) for l in out if l.strip() and l.strip() != "// ..."), default=0)
    out = [l[ind:] if l.strip() != "// ..." else "// ..." for l in out]
    body = html.escape("\n".join(out))
    body = re.sub(r"(//[^\n]*)", r'<span class="c">\1</span>', body)
    cap = attrs.get("cap", "") + ' <span style="font-weight:400;color:#6b7280">(' + attrs["src"] + ", lines " + attrs["lines"] + ")</span>"
    return '<pre class="code" data-cap="' + html.escape(cap, quote=True) + '"' + (' id="' + attrs["id"] + '"' if "id" in attrs else "") + ">" + body + "</pre>"

src = re.sub(r"<code-file\s+([^>]*)/>", code_file, src)
src = re.sub(r'<include src="([^"]+)"/>', lambda m: open(os.path.join(HERE, m.group(1)), encoding="utf-8").read(), src)

# ------------------------------------------------------------------ performance chart (inline SVG)
def perf_chart():
    data = [("Shooter \u2013 Neon Grid", 3.3), ("Shooter \u2013 Foundry", 2.7), ("Archery range", 2.5),
            ("Parachute (plane)", 3.0), ("Rat Shooter", 2.51), ("Farm Sniper", 2.78), ("Archery + guide (27 Nov)", 3.1)]
    W, H = 640, 290
    left, right, top, bottom = 170, 30, 28, 40
    xmax = 14.0
    pw, ph = W - left - right, H - top - bottom
    bh = ph / len(data)
    sx = lambda v: left + v / xmax * pw
    s = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="100%%" font-family="Segoe UI, Arial, sans-serif">' % (W, H)]
    for t in range(0, 15, 2):
        x = sx(t)
        s.append('<line x1="%.1f" y1="%d" x2="%.1f" y2="%d" stroke="#e5e7eb" stroke-width="1"/>' % (x, top, x, top + ph))
        s.append('<text x="%.1f" y="%d" font-size="10" fill="#6b7280" text-anchor="middle">%d</text>' % (x, top + ph + 14, t))
    s.append('<text x="%.1f" y="%d" font-size="10.5" fill="#374151" text-anchor="middle">Average frame time (ms) \u2013 lower is better</text>' % (left + pw / 2, H - 6))
    for i, (name, v) in enumerate(data):
        y = top + i * bh + bh * 0.22
        h = bh * 0.56
        s.append('<text x="%d" y="%.1f" font-size="10.5" fill="#111827" text-anchor="end" dominant-baseline="middle">%s</text>' % (left - 8, y + h / 2, name))
        w = sx(v) - left
        s.append('<path d="M%.1f %.1f h%.1f a4 4 0 0 1 4 4 v%.1f a4 4 0 0 1 -4 4 h-%.1f z" fill="#2a78d6"/>' % (left, y, w - 4, h - 8, w - 4))
        s.append('<text x="%.1f" y="%.1f" font-size="10" fill="#111827" dominant-baseline="middle">%.1f ms</text>' % (sx(v) + 5, y + h / 2, v))
    for val, lab in ((11.1, "90 Hz budget 11.1 ms"), (13.9, "72 Hz 13.9 ms")):
        x = sx(val)
        s.append('<line x1="%.1f" y1="%d" x2="%.1f" y2="%d" stroke="#374151" stroke-width="1.5" stroke-dasharray="4 3"/>' % (x, top - 4, x, top + ph))
        s.append('<text x="%.1f" y="%d" font-size="9.5" fill="#374151" text-anchor="%s">%s</text>' % (x - 4, top - 8, "end", lab))
    s.append('<line x1="%d" y1="%d" x2="%d" y2="%d" stroke="#9ca3af" stroke-width="1"/>' % (left, top, left, top + ph))
    s.append("</svg>")
    return "".join(s)

src = src.replace("<chart-perf/>", perf_chart())

# ------------------------------------------------------------------ numbering
refs = {}          # id -> label text
lof, lot, lol = [], [], []
toc = []           # (level, id, text, crit)
marker_n = [0]

def marker():
    marker_n[0] += 1
    return "QQMK%04dQQ" % marker_n[0]

out = []
pos = 0
chap = None            # current chapter label (e.g. "4" or "A")
sec = sub = fig = tab = lst = 0
token = re.compile(r'<h1 class="chap"([^>]*)>(.*?)</h1>|<h1 class="unnum"([^>]*)>(.*?)</h1>|<h2([^>]*)>(.*?)</h2>|<h3([^>]*)>(.*?)</h3>'
                   r'|<figure([^>]*)>(.*?)</figure>|<table([^>]*)data-cap="([^"]*)"([^>]*)>|<pre class="code" data-cap="([^"]*)"([^>]*)>', re.S)

def attr(s, name):
    m = re.search(name + r'="([^"]*)"', s or "")
    return m.group(1) if m else None

for m in token.finditer(src):
    out.append(src[pos:m.start()])
    pos = m.end()
    g0 = m.group(0)
    if g0.startswith('<h1 class="chap"'):
        a, title = m.group(1), m.group(2)
        chap = attr(a, "data-chap")
        crit = attr(a, "data-crit")
        hid = attr(a, "id") or "ch" + chap
        sec = sub = fig = tab = lst = 0
        mk = marker()
        kind = "Appendix" if chap.isalpha() else "Chapter"
        refs[hid] = "%s %s" % (kind, chap)
        crit_html = '<span class="crit">Rubric Criterion %s of 10</span>' % crit if crit else ""
        out.append('<h1 class="chap" id="%s"><span class="cnum">%s %s</span>%s<span style="font-size:1px;color:#fff">%s</span>%s</h1>'
                   % (hid, kind, chap, title, mk, ("<br>" + crit_html) if crit else ""))
        toc.append((1, hid, "%s %s \u2013 %s" % (kind, chap, re.sub("<[^>]+>", "", title)), crit, mk))
    elif g0.startswith('<h1 class="unnum"'):
        a, title = m.group(3), m.group(4)
        hid = attr(a, "id")
        mk = marker()
        out.append('<h1 class="unnum" id="%s">%s<span style="font-size:1px;color:#fff">%s</span></h1>' % (hid, title, mk))
        if attr(a, "data-toc") != "no":
            toc.append((1, hid, re.sub("<[^>]+>", "", title), None, mk))
    elif g0.startswith('<h2'):
        a, title = m.group(5), m.group(6)
        if chap is None or attr(a, "data-nonum"):
            out.append(m.group(0)); continue
        sec += 1; sub = 0
        num = "%s.%d" % (chap, sec)
        hid = attr(a, "id") or "s" + num.replace(".", "-")
        refs[hid] = "Section " + num
        mk = marker()
        out.append('<h2 id="%s">%s&nbsp;&nbsp;%s<span style="font-size:1px;color:#fff">%s</span></h2>' % (hid, num, title, mk))
        toc.append((2, hid, num + "  " + re.sub("<[^>]+>", "", title), None, mk))
    elif g0.startswith('<h3'):
        a, title = m.group(7), m.group(8)
        if chap is None:
            out.append(m.group(0)); continue
        sub += 1
        num = "%s.%d.%d" % (chap, sec, sub)
        hid = attr(a, "id") or "s" + num.replace(".", "-")
        refs[hid] = "Section " + num
        out.append('<h3 id="%s">%s&nbsp;&nbsp;%s</h3>' % (hid, num, title))
    elif g0.startswith('<figure'):
        a, inner = m.group(9), m.group(10)
        if "data-nonum" in (a or ""):
            out.append(m.group(0)); continue
        fig += 1
        num = "%s.%d" % (chap, fig)
        fid = attr(a, "id") or "f" + num
        refs[fid] = "Figure " + num
        mk = marker()
        cap_m = re.search(r"<figcaption>(.*?)</figcaption>", inner, re.S)
        cap = cap_m.group(1).strip() if cap_m else ""
        inner = re.sub(r"<figcaption>.*?</figcaption>", '<figcaption><b>Figure %s</b> \u2013 %s<span style="font-size:1px;color:#fff">%s</span></figcaption>' % (num, cap, mk), inner, flags=re.S)
        a2 = re.sub(r'\sid="[^"]*"', "", a or "")
        out.append('<figure id="%s"%s>%s</figure>' % (fid, a2, inner))
        lof.append((fid, "Figure " + num, re.sub("<[^>]+>", "", cap), mk))
    elif g0.startswith('<table'):
        a1, cap, a2 = m.group(11), m.group(12), m.group(13)
        tab += 1
        num = "%s.%d" % (chap, tab)
        tid = attr(a1 + a2, "id") or "t" + num
        refs[tid] = "Table " + num
        mk = marker()
        out.append('<div class="tcap"><b>Table %s</b> \u2013 %s<span style="font-size:1px;color:#fff">%s</span></div><table%s%s>' % (num, cap, mk, a1, a2))
        lot.append((tid, "Table " + num, re.sub("<[^>]+>", "", html.unescape(cap)), mk))
    elif g0.startswith('<pre'):
        cap, a = m.group(14), m.group(15)
        lst += 1
        num = "%s.%d" % (chap, lst)
        lid = attr(a, "id") or "l" + num
        refs[lid] = "Listing " + num
        mk = marker()
        capu = html.unescape(cap)
        out.append('<div class="lcap"><b>Listing %s</b> \u2013 %s<span style="font-size:1px;color:#fff">%s</span></div><pre class="code"%s>' % (num, capu, mk, a))
        lol.append((lid, "Listing " + num, re.sub("<[^>]+>", "", capu), mk))
out.append(src[pos:])
body = "".join(out)

missing = set()
def ref(m):
    k = m.group(1)
    if k not in refs:
        missing.add(k)
        return "??" + k
    return '<a href="#%s">%s</a>' % (k, refs[k])
body = re.sub(r"\[\[([\w\-.]+)\]\]", ref, body)
if missing:
    print("MISSING REFS:", sorted(missing))

def list_html(items, pages):
    rows = []
    for (i, lab, text, mk) in items:
        rows.append('<div class="row"><span class="t"><b>%s</b>&nbsp; %s</span><span class="dots"></span><span class="p">%s</span></div>'
                    % (lab, html.escape(html.unescape(text)), pages.get(mk, "000")))
    return '<div class="toc lof">' + "".join(rows) + "</div>"

def toc_html(pages):
    rows = []
    for (lvl, hid, text, crit, mk) in toc:
        c = '<span class="crit">[Criterion %s]</span>' % crit if crit else ""
        rows.append('<div class="row l%d"><span class="t"><a href="#%s">%s</a>%s</span><span class="dots"></span><span class="p">%s</span></div>'
                    % (lvl, hid, html.escape(html.unescape(text)), c, pages.get(mk, "000")))
    return '<div class="toc">' + "".join(rows) + "</div>"

css = open(os.path.join(HERE, "style.css"), encoding="utf-8").read()
MERMAID_JS = "file:///" + os.path.join(HERE, "vendor", "mermaid-11.4.1.min.js").replace("\\", "/")
MERMAID = """<script src="MERMAID_JS"></script>
<script>
mermaid.initialize({ startOnLoad: false, theme: "base", securityLevel: "loose",
  flowchart: { htmlLabels: true, curve: "basis", nodeSpacing: 28, rankSpacing: 38, padding: 8 },
  sequence: { useMaxWidth: true, actorMargin: 40, messageFontSize: 13 },
  themeVariables: { fontFamily: "Segoe UI, Arial, sans-serif", fontSize: "15px", primaryColor: "#e6f6fb", primaryBorderColor: "#0e6e8c",
    primaryTextColor: "#0b2a38", lineColor: "#47606e", secondaryColor: "#fbeaf3", tertiaryColor: "#f4f6f8", clusterBkg: "#f7fafc", clusterBorder: "#9fb3c0",
    edgeLabelBackground: "#ffffff", noteBkgColor: "#fff8eb", noteBorderColor: "#d97706" } });
mermaid.run({ querySelector: ".mermaid", suppressErrors: true }).then(function () { document.body.setAttribute("data-mermaid", "done"); });
</script>""".replace("MERMAID_JS", MERMAID_JS)

def render(pages):
    doc = body.replace("<toc-here/>", toc_html(pages)).replace("<lof-here/>", list_html(lof, pages)) \
              .replace("<lot-here/>", list_html(lot, pages)).replace("<lol-here/>", list_html(lol, pages))
    page = ('<!doctype html><html lang="en"><head><meta charset="utf-8"><title>NEON BREACH \u2013 ESE Project Report</title><style>%s</style></head><body>%s%s</body></html>'
            % (css, doc, MERMAID))
    hp = os.path.join(BUILD, "report.html")
    open(hp, "w", encoding="utf-8").write(page)
    pdf = os.path.join(BUILD, "report.pdf")
    if os.path.exists(pdf):
        os.remove(pdf)
    url = "file:///" + hp.replace("\\", "/")
    subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--no-pdf-header-footer", "--run-all-compositor-stages-before-draw",
                    "--virtual-time-budget=45000", "--user-data-dir=" + os.path.join(__import__("tempfile").gettempdir(), "nb-report-edge-profile"), "--print-to-pdf=" + pdf, url], check=True, capture_output=True, timeout=600)
    return pdf

def page_map(pdf):
    import fitz   # PyMuPDF: much faster than pdfplumber on this image-heavy document
    found = {}
    doc = fitz.open(pdf)
    n = doc.page_count
    for i, p in enumerate(doc):
        t = p.get_text() or ""
        for mk in re.findall(r"QQMK\d{4}QQ", re.sub(r"\s+", "", t)):
            found.setdefault(mk, str(i + 1))
    doc.close()
    return found, n

pdf = render({})
pages, n = page_map(pdf)
print("pass 1:", n, "pages,", len(pages), "of", marker_n[0], "markers located")
pdf = render(pages)
pages2, n2 = page_map(pdf)
moved = [k for k in pages if pages2.get(k) != pages[k]]
print("pass 2:", n2, "pages; markers moved:", len(moved))
if moved:
    pdf = render(pages2)
    pages3, n3 = page_map(pdf)
    print("pass 3:", n3, "pages; moved again:", len([k for k in pages2 if pages3.get(k) != pages2[k]]))
import shutil
shutil.copyfile(pdf, OUT_PDF)
print("figures", len(lof), "tables", len(lot), "listings", len(lol), "->", OUT_PDF, round(os.path.getsize(OUT_PDF) / 1e6, 1), "MB")
