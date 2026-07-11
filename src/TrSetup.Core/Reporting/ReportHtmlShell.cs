namespace TrSetup.Core.Reporting;

/// <summary>
/// The shared self-contained HTML doc-shell snippets (palette, theme bootstrap, behaviour JS)
/// every rendered HTML in this framework uses — mirrored from
/// <c>.tfcore/templates/v4custom/html-render-shell.md</c> (§2 CSS, §3 head script, §7 JS).
/// The report has no Mermaid diagrams, so the diagram CSS/JS and CDN scripts are omitted:
/// the exported HTML is fully self-contained with zero external requests.
/// </summary>
internal static class ReportHtmlShell
{
    /// <summary>Flash-free theme bootstrap placed in <c>&lt;head&gt;</c>: saved choice wins, else time of day.</summary>
    internal const string HeadThemeScript = """
        (function(){
          try{
            var t = localStorage.getItem('tf-theme');
            if(t!=='light' && t!=='dark'){ var h = new Date().getHours(); t = (h>=7 && h<19) ? 'light' : 'dark'; }
            document.documentElement.setAttribute('data-theme', t);
          }catch(e){ document.documentElement.setAttribute('data-theme','light'); }
        })();
        """;

    /// <summary>Shell §2 CSS (verbatim palette + base rules; diagram rules omitted — no Mermaid in reports).</summary>
    internal const string Css = """
        /* LIGHT is the default theme — a warm off-white, easy on the eyes (never bright white). */
        :root{
          --bg:#f4f1e9; --panel:#efe9db; --panel2:#e7e0cd; --line:#d8cfb6;
          --ink:#2e2a22; --muted:#6f6857; --accent:#2f6f9f; --accent2:#3f7d54;
          --warn:#9a6b15; --danger:#b03a52; --ok:#3f7d54;
          --code-bg:#ece5d3; --code-ink:#332f26; --row-alt:#eae2cf;
          --h3:#3a352b; --h4:#2f6f9f; --quote:#4a4537; --cta-bg:#e6eef5;
          --mono:ui-monospace,SFMono-Regular,Menlo,Consolas,"Liberation Mono",monospace;
          --sans:ui-sans-serif,system-ui,-apple-system,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;
        }
        /* DARK — softened near-black (warmer / less harsh than pure #0f1115). */
        html[data-theme="dark"]{
          --bg:#11141a; --panel:#171b23; --panel2:#1e2430; --line:#2b3240;
          --ink:#dfe3ea; --muted:#99a2b1; --accent:#7cc4ff; --accent2:#a6e3a1;
          --warn:#f9c74f; --danger:#f38ba8; --ok:#a6e3a1;
          --code-bg:#0c1016; --code-ink:#d8dde8; --row-alt:#141821;
          --h3:#cfd5e3; --h4:#7cc4ff; --quote:#cfd5e3; --cta-bg:#0d2030;
        }
        *{box-sizing:border-box}
        html,body{margin:0;padding:0;background:var(--bg);color:var(--ink);font-family:var(--sans);line-height:1.55;font-size:15px}
        a{color:var(--accent);text-decoration:none}
        a:hover{text-decoration:underline}
        code{font-family:var(--mono);font-size:13px;background:var(--code-bg);color:var(--code-ink);padding:1px 5px;border-radius:4px;border:1px solid var(--line)}
        pre{font-family:var(--mono);font-size:12.5px;background:var(--code-bg);border:1px solid var(--line);border-radius:8px;padding:12px 14px;overflow:auto;color:var(--code-ink);position:relative}
        pre code{background:none;border:none;padding:0}
        .copy{position:absolute;top:8px;right:8px;background:var(--panel2);color:var(--muted);border:1px solid var(--line);border-radius:6px;padding:3px 8px;font-size:11px;font-family:var(--sans);cursor:pointer}
        .copy:hover{color:var(--accent);border-color:var(--accent)}
        .theme-toggle{position:fixed;top:12px;right:14px;z-index:10000;background:var(--panel);color:var(--ink);border:1px solid var(--line);border-radius:8px;padding:6px 12px;font-size:12.5px;font-family:var(--sans);cursor:pointer;box-shadow:0 1px 4px rgba(0,0,0,.15)}
        .theme-toggle:hover{color:var(--accent);border-color:var(--accent)}

        .layout{display:grid;grid-template-columns:260px 1fr;min-height:100vh}
        .layout.no-toc{grid-template-columns:1fr}
        nav.side{position:sticky;top:0;height:100vh;overflow-y:auto;border-right:1px solid var(--line);background:var(--panel);padding:18px 14px}
        nav.side h1{font-size:14px;margin:0 0 4px;letter-spacing:.5px;color:var(--ink)}
        nav.side .sub{font-size:11.5px;color:var(--muted);margin-bottom:14px}
        nav.side ol,nav.side ul{list-style:none;padding:0;margin:0}
        nav.side li{font-size:13px;margin:2px 0}
        nav.side li a{display:block;padding:5px 8px;color:var(--ink);border-radius:6px}
        nav.side li a:hover{background:var(--panel2);text-decoration:none}
        nav.side .group{font-size:11px;color:var(--muted);text-transform:uppercase;letter-spacing:.7px;margin:14px 8px 4px}

        main{padding:28px 40px 80px;max-width:1100px}
        .layout.no-toc main{margin:0 auto}

        h1{font-size:28px;margin:0 0 6px}
        h2{font-size:22px;margin:32px 0 10px;padding-bottom:6px;border-bottom:1px solid var(--line);scroll-margin-top:16px}
        h3{font-size:17px;margin:22px 0 6px;color:var(--h3);scroll-margin-top:16px}
        h4{font-size:14px;margin:14px 0 4px;color:var(--h4);scroll-margin-top:16px}
        .subtitle{color:var(--muted);font-size:14px;margin-top:-4px;margin-bottom:18px}
        p{margin:6px 0 10px}
        blockquote{border-left:3px solid var(--accent);background:var(--panel);margin:8px 0;padding:8px 14px;color:var(--quote)}
        ul,ol{padding-left:22px}
        li{margin:2px 0}

        table{border-collapse:collapse;width:100%;font-size:13.5px;margin:8px 0}
        th,td{border:1px solid var(--line);padding:8px 10px;text-align:left;vertical-align:top}
        th{background:var(--panel2);color:var(--h3);font-weight:600}
        tr:nth-child(even) td{background:var(--row-alt)}

        hr{border:none;border-top:1px solid var(--line);margin:24px 0}

        .toc-inline{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:10px 16px;margin:14px 0 24px}
        .toc-inline > div{font-size:11.5px;color:var(--muted);text-transform:uppercase;letter-spacing:.5px;margin-bottom:6px}
        .toc-inline ol,.toc-inline ul{margin:4px 0;padding-left:18px}
        .toc-inline li{font-size:13px;margin:1px 0}
        .toc-inline a{color:var(--h3)}

        .status-pass{color:var(--ok);font-weight:600}
        .status-warn{color:var(--warn);font-weight:600}
        .status-fail{color:var(--danger);font-weight:600}
        .status-na{color:var(--muted);font-weight:600}
        .row-meta{font-size:12.5px;color:var(--muted);margin:2px 0 6px}
        .counts{font-size:13px;color:var(--muted);font-weight:400;margin-left:10px}

        @media(max-width:900px){.layout{grid-template-columns:1fr}nav.side{position:static;height:auto}}
        """;

    /// <summary>Shell §7 JS (theme toggle + copy buttons; Mermaid wiring omitted — no diagrams in reports).</summary>
    internal const string BodyScript = """
        (function(){
          var btn = document.getElementById('themeToggle');
          if(!btn) return;
          var relabel = function(){
            var dark = document.documentElement.getAttribute('data-theme') === 'dark';
            btn.textContent = dark ? '☀ Light' : '☾ Dark';
          };
          relabel();
          btn.addEventListener('click', function(){
            var next = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
            try{ localStorage.setItem('tf-theme', next); }catch(e){}
            location.reload();
          });
        })();

        document.querySelectorAll('pre:not(.mermaid)').forEach(pre => {
          const btn = document.createElement('button');
          btn.className = 'copy';
          btn.textContent = 'copy';
          btn.addEventListener('click', () => {
            const text = pre.querySelector('code') ? pre.querySelector('code').innerText : pre.innerText;
            navigator.clipboard.writeText(text).then(() => {
              btn.textContent = 'copied';
              setTimeout(() => { btn.textContent = 'copy'; }, 1200);
            });
          });
          pre.appendChild(btn);
        });
        """;
}
