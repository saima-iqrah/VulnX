using System.Text;
using System.Text.RegularExpressions;
using VulnX.Application.Scans;
using VulnX.Domain.Entities;
using VulnX.Scanner.Engine;

namespace VulnX.Infrastructure.Ai;

internal sealed record BuiltInAnswer(string Reply, IReadOnlyList<string> Suggestions);

/// Rule-based assistant used when no AI model is configured (or the model does not answer).
/// It answers from the stored scan, the scanner catalog, a small security glossary and VulnX help texts.
internal static class BuiltInAssistant
{
    private static readonly string[] WithScan = { "What should I fix first?", "Explain my score", "Summarize my latest scan" };
    private static readonly string[] NoScan = { "How do I start a scan?", "What does the grade mean?", "What is XSS?" };

    private static bool Has(string text, string term)
    {
        var plural = term.Length >= 4 ? "s?" : "";
        return Regex.IsMatch(text, "(?<![a-z0-9])" + Regex.Escape(term) + plural + "(?![a-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
    }

    private static bool Any(string text, params string[] terms) => terms.Any(t => Has(text, t));

    private static readonly (string[] Words, string[] Codes)[] Topics =
    {
        (new[] { "content-security-policy", "csp" }, new[] { "HDR-001" }),
        (new[] { "hsts", "strict-transport-security" }, new[] { "HDR-002", "HDR-003" }),
        (new[] { "clickjacking", "x-frame-options", "frame-ancestors", "iframe" }, new[] { "HDR-004" }),
        (new[] { "nosniff", "x-content-type-options", "mime sniffing" }, new[] { "HDR-005" }),
        (new[] { "referrer", "referrer-policy" }, new[] { "HDR-006" }),
        (new[] { "permissions-policy" }, new[] { "HDR-007" }),
        (new[] { "security header" }, new[] { "HDR-001", "HDR-002", "HDR-004", "HDR-005", "HDR-006", "HDR-007" }),
        (new[] { "certificate", "expired certificate" }, new[] { "TLS-002", "TLS-003" }),
        (new[] { "legacy tls", "old tls", "tls version", "tls handshake" }, new[] { "TLS-004", "TLS-006" }),
        (new[] { "redirect to https", "http to https", "plain http" }, new[] { "TLS-001", "TLS-005" }),
        (new[] { "cookie", "httponly", "samesite", "secure flag" }, new[] { "CKE-001", "CKE-002", "CKE-003" }),
        (new[] { "cors", "cross-origin", "access-control-allow-origin" }, new[] { "COR-001", "COR-002", "COR-003" }),
        (new[] { "server header", "x-powered-by", "version disclosure", "banner" }, new[] { "INF-001", "INF-002" }),
        (new[] { ".git", "git repository" }, new[] { "INF-003" }),
        (new[] { ".env", "environment file" }, new[] { "INF-004" }),
        (new[] { "mixed content" }, new[] { "HTM-001" }),
        (new[] { "sri", "subresource integrity" }, new[] { "HTM-002" }),
        (new[] { "form action", "insecure form" }, new[] { "HTM-003" })
    };

    private static readonly (string[] Terms, string Title, string Text)[] Glossary =
    {
        (new[] { "xss", "cross-site scripting" }, "Cross-site scripting (XSS)",
            "XSS happens when a site puts untrusted input into a page without proper encoding, so an attacker's script runs in another user's browser. " +
            "Defend by encoding output for the right context, validating input, using a strict Content-Security-Policy and HttpOnly cookies. " +
            "VulnX does not test for XSS itself; it only checks defensive settings such as CSP."),
        (new[] { "sql injection", "sqli" }, "SQL injection",
            "SQL injection happens when user input is concatenated into a database query so the input changes the query. " +
            "Defend with parameterized queries or an ORM (such as Entity Framework Core), least-privilege database accounts and input validation. VulnX never sends injection payloads."),
        (new[] { "csrf", "cross-site request forgery" }, "Cross-site request forgery (CSRF)",
            "CSRF tricks a logged-in user's browser into sending an unwanted request to a site. " +
            "Defend with SameSite cookies, anti-forgery tokens, and by not using cookies for authentication of state-changing API calls."),
        (new[] { "ssrf", "server-side request forgery" }, "Server-side request forgery (SSRF)",
            "SSRF makes a server fetch a URL chosen by an attacker, often to reach internal systems such as the cloud metadata address 169.254.169.254. " +
            "Defend with allow-lists, blocking private IP ranges after DNS resolution, and refusing redirects to other hosts. VulnX's own scanner uses these protections."),
        (new[] { "owasp", "top 10" }, "OWASP",
            "OWASP is a non-profit that publishes free web-security guidance. The OWASP Top 10 lists the most critical web application risks, such as broken access control, cryptographic failures and injection. " +
            "VulnX findings link to OWASP and MDN references."),
        (new[] { "jwt", "json web token" }, "JSON Web Token (JWT)",
            "A JWT is a signed token that carries claims such as the user id and role. The server verifies the signature with a secret key, so nobody can forge a token without that key. " +
            "Keep access tokens short-lived and the signing key secret."),
        (new[] { "vulnerability scan", "penetration test", "pentest" }, "Vulnerability scan vs penetration test",
            "A vulnerability scan is automated and looks for known weaknesses and misconfigurations. A penetration test is performed by a person who actively tries to exploit weaknesses. " +
            "VulnX is a passive scanner: it never exploits anything, so a clean result is not proof that a site is secure."),
        (new[] { "denial of service", "ddos", "dos attack" }, "Denial of service",
            "A denial-of-service attack tries to make a service unavailable by overwhelming it. Defend with rate limiting, caching or a CDN, autoscaling and a web application firewall. " +
            "VulnX never performs DoS: scans are rate-limited and limited by a request budget."),
        (new[] { "2fa", "mfa", "two-factor", "multi-factor" }, "Two-factor authentication",
            "Two-factor authentication asks for a second proof (for example an app code) in addition to the password, so a stolen password alone is not enough."),
        (new[] { "brute force", "brute-force", "password guessing" }, "Brute-force attacks",
            "Brute force means guessing many passwords automatically. Defend with account lockout, rate limiting, strong passwords and two-factor authentication. VulnX locks an account for 15 minutes after 5 failed sign-ins."),
        (new[] { "rate limiting", "throttling" }, "Rate limiting",
            "Rate limiting caps how many requests a client can make in a time window. It slows brute-force attacks, scraping and abuse. VulnX limits sign-in, scans, AI questions and report verification."),
        (new[] { "waf", "web application firewall" }, "Web application firewall (WAF)",
            "A WAF filters HTTP traffic in front of a site and blocks known attack patterns. It is a useful extra layer, but it does not replace fixing the vulnerabilities themselves."),
        (new[] { "password hashing", "hashing", "bcrypt", "salt" }, "Password hashing",
            "Passwords must never be stored in plain text. They are run through a slow, salted hash function (such as PBKDF2, bcrypt or Argon2) so a stolen database does not reveal them. VulnX uses ASP.NET Core Identity for this."),
        (new[] { "supply chain", "dependency", "npm audit", "outdated library" }, "Dependency and supply-chain risk",
            "Third-party packages can contain vulnerabilities or be tampered with. Keep dependencies updated, use lock files, run audit tools (npm audit, dotnet list package --vulnerable) and avoid loading scripts from untrusted hosts without Subresource Integrity."),
        (new[] { "https", "tls", "ssl", "encryption in transit" }, "HTTPS and TLS",
            "HTTPS encrypts traffic between the browser and the server using TLS, so others on the network cannot read or change it. " +
            "Use a valid certificate from a trusted authority, allow only TLS 1.2 and 1.3, redirect HTTP to HTTPS and send the Strict-Transport-Security header.")
    };

    private static readonly (string[] Terms, string Text)[] Help =
    {
        (new[] { "verify report", "report verification", "verification code", "verify a report" },
            "Every PDF report has a report number and a verification code. Open the **Verify report** page (/verify-report) and enter them. The check fails if the report or its findings were changed."),
        (new[] { "download report", "pdf", "report" },
            "Open a completed scan and press **Download PDF report**. The PDF has the executive summary, severity table, detailed findings with evidence, a remediation checklist and a verification code."),
        (new[] { "start a scan", "new scan", "run a scan", "how to scan", "how do i scan", "scan my site", "scan a website", "scan a url", "scan website" },
            "Go to **New scan**, enter a public URL you own or are authorized to test, choose a profile, tick the authorization box and press **Start scan**. Progress updates live and the findings appear when it finishes."),
        (new[] { "scan profile", "quick scan", "deep scan", "standard scan", "difference between profiles" },
            "**Quick**: security headers, TLS and cookies. **Standard**: adds CORS, information disclosure and HTML content checks. **Deep**: the same checks with a larger request budget and longer time limit."),
        (new[] { "localhost", "127.0.0.1", "private ip", "internal network", "why blocked", "blocked" },
            "Localhost, private networks and internal host names are blocked on purpose (SSRF protection): the scanner must never be used to reach internal systems. Scan a public site you are authorized to test."),
        (new[] { "authorized", "authorization", "legal", "permission", "illegal" },
            "Only scan sites you own or have explicit written permission to test. VulnX asks you to confirm this for every scan and records the confirmation."),
        (new[] { "compare", "comparison" },
            "In **Scan history** tick two completed scans and press **Compare selected**, or open **Compare scans**. You see new, resolved and unchanged findings and the score change."),
        (new[] { "history", "tag", "label", "filter scans", "search scans" },
            "**Scan history** lets you search by URL or label, filter by status or tag, compare scans and delete old ones. Open a scan to add a label, notes and tags."),
        (new[] { "notification", "bell" },
            "The bell at the top shows unread notifications: finished scans, serious findings and announcements. Open **Notifications** to read or delete them."),
        (new[] { "change password", "reset password", "forgot password", "my profile" },
            "Use **Profile** to change your name or password. If you forgot your password, use **Forgot password** on the sign-in page; the link arrives by email."),
        (new[] { "delete scan", "remove scan" },
            "Open the scan (or use Scan history) and press **Delete**. Running scans must be cancelled first. Deleting a scan also deletes its findings and report."),
        (new[] { "language model", "chatgpt", "llm", "are you ai", "are you an ai", "are you real" },
            "When an AI model is connected I can answer open-ended questions. Without one I use a built-in knowledge base about your scans, security topics and VulnX. Either way, findings only come from the scanner.")
    };

    /// Short trusted notes for a question, used to ground a language model (a light retrieval step).
    public static IReadOnlyList<string> Retrieve(string message)
    {
        var notes = new List<string>();
        var m = message.Trim();

        foreach (var code in Topics.Where(t => Any(m, t.Words)).SelectMany(t => t.Codes).Distinct().Take(3))
        {
            var e = VulnerabilityCatalog.Get(code);
            notes.Add($"{e.Code} {e.Name} (default severity {e.Severity}): {e.Description} Impact: {e.Impact} Fix: {e.Recommendation}");
        }
        foreach (var g in Glossary.Where(g => Any(m, g.Terms)).Take(2)) notes.Add($"{g.Title}: {g.Text}");
        foreach (var h in Help.Where(h => Any(m, h.Terms)).Take(2)) notes.Add(h.Text);
        return notes.Take(5).ToList();
    }

    public static BuiltInAnswer Answer(string message, Scan? scan, ScanResult? focus)
    {
        var m = message.Trim();
        var suggestions = scan is null ? NoScan : WithScan;
        BuiltInAnswer R(string text) => new(text, suggestions);

        if (Any(m, "thanks", "thank you", "thx", "dhonnobad"))
            return R("You're welcome! Ask me anything else about your scans or web security.");

        if (m.Length < 30 && Any(m, "hi", "hello", "hey", "salam", "assalamualaikum"))
            return R("Hello! I'm the VulnX assistant. I can explain your scan findings, answer web-security questions and show you how to use VulnX. What would you like to know?");

        if (Any(m, "help", "what can you do", "who are you"))
            return R("I can explain your scan results, tell you what to fix first, explain your score, answer questions about web security (XSS, CORS, cookies, HTTPS, headers...) and show you how to use VulnX. Try one of the suggestions below.");

        // a finding code such as HDR-001
        var codeMatch = Regex.Match(m, @"\b([a-z]{3})-(\d{3})\b", RegexOptions.IgnoreCase);
        if (codeMatch.Success)
        {
            var entry = VulnerabilityCatalog.All.FirstOrDefault(e => e.Code == codeMatch.Value.ToUpperInvariant());
            if (entry is not null) return R(FormatEntry(entry, scan));
        }

        // topics covered by scanner checks
        var codes = Topics.Where(t => Any(m, t.Words)).SelectMany(t => t.Codes).Distinct().ToList();
        if (codes.Count > 0)
        {
            var entries = codes.Select(c => VulnerabilityCatalog.Get(c)).ToList();
            if (entries.Count <= 3)
                return R(string.Join("\n\n---\n\n", entries.Select(e => FormatEntry(e, scan))));

            var list = string.Join("\n", entries.Select(e => $"- **{e.Name}** ({e.Code}, {e.Severity})"));
            return R("These checks are related to your question:\n" + list + "\n\nAsk about one of them (for example **HDR-001**) for details and how to fix it.");
        }

        // questions about the user's scan
        if (Any(m, "fix first", "priority", "priorities", "most important", "where to start", "what should i fix", "worst", "most serious", "biggest"))
        {
            if (scan is null) return R(NoScanText());
            var top = scan.Results.OrderByDescending(r => r.Severity).ThenBy(r => r.Definition.Code).Take(3).ToList();
            if (top.Count == 0) return R($"Your latest scan of {scan.Target.Hostname} reported no issues, so there is nothing to fix right now. Keep scanning after every deployment.");
            return R("Start with these:\n" + string.Join("\n", top.Select(r => $"- **{r.Severity}** {r.Definition.Code} {r.Title}: {r.Recommendation}")) +
                     "\n\nAfter fixing, run the scan again to confirm.");
        }

        if (Any(m, "how many", "count", "number of") && Any(m, "finding", "issue", "vulnerability", "vulnerabilities", "critical", "high", "medium", "low"))
        {
            if (scan is null) return R(NoScanText());
            var counts = string.Join(", ", scan.Results.GroupBy(r => r.Severity).OrderByDescending(g => g.Key).Select(g => $"{g.Count()} {g.Key}"));
            return R(scan.Results.Count == 0
                ? $"Your latest scan of {scan.Target.Hostname} reported no findings."
                : $"Your latest scan of {scan.Target.Hostname} reported {scan.Results.Count} finding(s): {counts}.");
        }

        if (Any(m, "score", "grade", "rating", "marks"))
        {
            const string formula =
                "The score starts at 100 and subtracts a fixed amount for each distinct finding type: Critical 25, High 15, Medium 7, Low 3, Informational 0 (never below 0). " +
                "Grades: A 90+, B 75-89, C 60-74, D 40-59, F below 40. The scanner calculates it, so identical findings always give the same score; AI never changes it.";
            return R(scan is null ? formula
                : $"Your latest scan of {scan.Target.Hostname} scored {scan.Score}/100 (grade {ScoreCalculator.GradeFor(scan.Score ?? 0)}).\n\n{formula}");
        }

        if (Any(m, "summary", "summarize", "summarise", "overview", "how secure", "how is my site", "my result"))
        {
            if (scan is null) return R(NoScanText());
            var s = TemplateExplainer.Summarize(scan, string.Empty);
            return R(s.Summary + "\n\nPriorities:\n" + string.Join("\n", s.Priorities.Select(p => "- " + p)));
        }

        // a finding the user focused on
        if (focus is not null)
        {
            var e = TemplateExplainer.Explain(focus, string.Empty);
            return R($"{e.Summary}\n\nWhy it matters: {e.WhyItMatters}\n\nHow to fix:\n" + string.Join("\n", e.FixSteps.Select(x => "- " + x)));
        }

        foreach (var g in Glossary)
            if (Any(m, g.Terms))
                return R($"**{g.Title}**\n{g.Text}");

        foreach (var h in Help)
            if (Any(m, h.Terms))
                return R(h.Text);

        return R("I'm the built-in assistant, so I can answer questions about your scan results, common web-security topics (XSS, CSRF, CORS, HSTS, cookies, TLS...) and how to use VulnX. " +
                 "I could not match this question. For open-ended questions, an administrator can connect an AI model (see the Ai settings in the README).");
    }

    private static string NoScanText() =>
        "You do not have a completed scan yet. Go to **New scan**, scan a site you are authorized to test, and then ask me about the results.";

    private static string FormatEntry(CatalogEntry e, Scan? scan)
    {
        var sb = new StringBuilder();
        sb.Append($"**{e.Name}** ({e.Code}, default severity {e.Severity})\n{e.Description}\n\nWhy it matters: {e.Impact}\n\nHow to fix: {e.Recommendation}");
        if (scan is not null)
        {
            var hit = scan.Results.FirstOrDefault(r => r.Definition.Code == e.Code);
            sb.Append("\n\n");
            sb.Append(hit is not null
                ? $"In your latest scan of {scan.Target.Hostname} this was reported at {hit.AffectedUrl}."
                : $"Your latest scan of {scan.Target.Hostname} did not report this issue.");
        }
        return sb.ToString();
    }
}

