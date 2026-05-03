using Microsoft.Playwright;
using CouncilChatbotPrototype.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CouncilChatbotPrototype.Services;

public class PlaywrightService
{
    private readonly IConfiguration _config;
    private readonly string _targetUrl;
    private readonly bool _headless;
    private readonly int _waitAfterSubmitMs;
    private readonly int _timeoutMs;

    public PlaywrightService(IConfiguration config)
    {
        _config = config;
        _targetUrl = _config["Playwright:TargetUrl"]
            ?? "https://onlineforms.bradford.gov.uk/ufs/collectiondates.eb";

        _headless = bool.TryParse(_config["Playwright:Headless"], out var parsedHeadless)
            ? parsedHeadless
            : true;

        _waitAfterSubmitMs = int.TryParse(_config["Playwright:WaitAfterSubmitMs"], out var parsedWait)
            ? parsedWait
            : 3000;

        _timeoutMs = int.TryParse(_config["Playwright:TimeoutMs"], out var parsedTimeout)
            ? parsedTimeout
            : 60000;
    }

    public async Task<AddressLookupResult> GetAddressesByPostcodeAsync(string postcode)
    {
        var result = new AddressLookupResult
        {
            Postcode = postcode?.Trim() ?? ""
        };

        if (string.IsNullOrWhiteSpace(result.Postcode))
        {
            result.Error = "Postcode is required.";
            return result;
        }

        try
        {
            using var playwright = await Playwright.CreateAsync();

            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _headless
            });

            var page = await browser.NewPageAsync();
            page.SetDefaultTimeout(_timeoutMs);

            await page.GotoAsync(_targetUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = _timeoutMs
            });

            await FillPostcodeAndSubmitAsync(page, result.Postcode);

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForTimeoutAsync(_waitAfterSubmitMs);

            var addresses = await ExtractAddressButtonsAsync(page);

            if (addresses.Count == 0)
            {
                var bodyText = await SafeGetBodyTextAsync(page);

                Console.WriteLine("===== PAGE BODY AFTER POSTCODE SEARCH =====");
                Console.WriteLine(bodyText);
                Console.WriteLine("===========================================");

                result.Error = "No address buttons were returned for that postcode.";
                return result;
            }

            result.Addresses = addresses;
            return result;
        }
        catch (TimeoutException)
        {
            result.Error = "The postcode lookup timed out.";
            return result;
        }
        catch (Exception ex)
        {
            result.Error = $"Playwright error: {ex.Message}";
            return result;
        }
    }

    public async Task<string> GetBinResultForAddressAsync(string postcode, string selectedAddress)
    {
        if (string.IsNullOrWhiteSpace(postcode))
            return "Postcode is required.";

        if (string.IsNullOrWhiteSpace(selectedAddress))
            return "Address is required.";

        try
        {
            using var playwright = await Playwright.CreateAsync();

            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _headless
            });

            var page = await browser.NewPageAsync();
            page.SetDefaultTimeout(_timeoutMs);

            await page.GotoAsync(_targetUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = _timeoutMs
            });

            await FillPostcodeAndSubmitAsync(page, postcode.Trim());

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForTimeoutAsync(_waitAfterSubmitMs);

            var clickedAddress = await ClickAddressButtonAsync(page, selectedAddress.Trim());

            if (!clickedAddress)
                return $"Could not find a matching address button for: {selectedAddress}";

            // Wait for the page to settle after address selection — Bradford's form can
            // take a few seconds before the next button becomes interactive.
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForTimeoutAsync(_waitAfterSubmitMs);

            var pageBodyAfterAddress = await SafeGetBodyTextAsync(page);
            Console.WriteLine("===== PAGE BODY AFTER ADDRESS CLICK =====");
            Console.WriteLine(pageBodyAfterAddress);
            Console.WriteLine("=========================================");

            // Bradford's form (2026+) goes DIRECTLY to the results page after the user
            // clicks an address — there is no intermediate "Show collection dates" button.
            // Only attempt the button-click flow when we are NOT already on the results page.
            bool alreadyOnResultsPage = LooksLikeResultsPage(pageBodyAfterAddress);
            Console.WriteLine($"[FLOW] After address click — alreadyOnResultsPage={alreadyOnResultsPage}");

            if (!alreadyOnResultsPage)
            {
                // Give extra time for slow renders — bump to 3 s if the default is ≤2 s
                if (_waitAfterSubmitMs < 3000)
                    await page.WaitForTimeoutAsync(3000 - _waitAfterSubmitMs);

                var clickedShowDates = await ClickShowCollectionDatesAsync(page);

                if (!clickedShowDates)
                {
                    // Capture a FRESH page body at the moment of failure (not the stale pre-wait snapshot)
                    var freshBodyOnFailure = await SafeGetBodyTextAsync(page);
                    Console.WriteLine("===== BUTTON NOT FOUND — fresh page body at failure =====");
                    var freshLines = freshBodyOnFailure
                        .Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x));
                    foreach (var fl in freshLines)
                        Console.WriteLine($"  {fl}");
                    Console.WriteLine("=========================================================");
                    return
                        "We could not automatically retrieve your bin collection dates this time " +
                        "(the council's online form may have changed). " +
                        "Please check your dates directly at: " +
                        "https://www.bradford.gov.uk/bins-and-recycling/bin-collection-dates/";
                }

                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await page.WaitForTimeoutAsync(_waitAfterSubmitMs);
            }

            var resultText = await ExtractCollectionResultsAsync(page);

            Console.WriteLine("===== PAGE BODY AFTER SHOW COLLECTION DATES =====");
            Console.WriteLine(resultText);
            Console.WriteLine("=================================================");

            return string.IsNullOrWhiteSpace(resultText)
                ? "No bin collection information found."
                : resultText.Trim();
        }
        catch (TimeoutException)
        {
            return "The bin result lookup timed out.";
        }
        catch (Exception ex)
        {
            return $"Playwright error: {ex.Message}";
        }
    }

    private async Task FillPostcodeAndSubmitAsync(IPage page, string postcode)
    {
        var postcodeInput = page.Locator("input[type='text']").First;

        await postcodeInput.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = _timeoutMs
        });

        await postcodeInput.FillAsync(postcode);
        await postcodeInput.PressAsync("Tab");

        var exactFindButton = page.GetByRole(AriaRole.Button, new() { Name = "Find address" });

        if (await exactFindButton.CountAsync() > 0)
        {
            await exactFindButton.First.ClickAsync();
            return;
        }

        var genericButtons = page.Locator("button, input[type='submit'], input[type='button']");
        var count = await genericButtons.CountAsync();

        for (int i = 0; i < count; i++)
        {
            var text = await SafeGetElementTextAsync(genericButtons.Nth(i));

            if (text.Contains("find", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("address", StringComparison.OrdinalIgnoreCase))
            {
                await genericButtons.Nth(i).ClickAsync();
                return;
            }
        }

        throw new Exception("Could not find the 'Find address' button.");
    }

    private async Task<List<string>> ExtractAddressButtonsAsync(IPage page)
    {
        var addresses = new List<string>();

        var buttons = page.GetByRole(AriaRole.Button);
        var count = await buttons.CountAsync();

        for (int i = 0; i < count; i++)
        {
            var text = (await SafeGetElementTextAsync(buttons.Nth(i))).Trim();

            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (IsIgnoredButton(text))
                continue;

            if (!LooksLikeAddressOption(text))
                continue;

            addresses.Add(text);
        }

        if (addresses.Count > 0)
        {
            return addresses
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        var fallbackElements = page.Locator("button, a, input[type='button'], input[type='submit']");
        var fallbackCount = await fallbackElements.CountAsync();

        for (int i = 0; i < fallbackCount; i++)
        {
            var text = (await SafeGetElementTextAsync(fallbackElements.Nth(i))).Trim();

            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (IsIgnoredButton(text))
                continue;

            if (!LooksLikeAddressOption(text))
                continue;

            addresses.Add(text);
        }

        return addresses
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private async Task<bool> ClickAddressButtonAsync(IPage page, string selectedAddress)
    {
        var exactButton = page.GetByRole(AriaRole.Button, new()
        {
            Name = selectedAddress,
            Exact = true
        });

        if (await exactButton.CountAsync() > 0)
        {
            await exactButton.First.ClickAsync();
            return true;
        }

        var buttons = page.GetByRole(AriaRole.Button);
        var count = await buttons.CountAsync();

        for (int i = 0; i < count; i++)
        {
            var text = (await SafeGetElementTextAsync(buttons.Nth(i))).Trim();

            if (string.Equals(
                NormalizeForComparison(text),
                NormalizeForComparison(selectedAddress),
                StringComparison.OrdinalIgnoreCase))
            {
                await buttons.Nth(i).ClickAsync();
                return true;
            }
        }

        var fallbackElements = page.Locator("button, a, input[type='button'], input[type='submit']");
        var fallbackCount = await fallbackElements.CountAsync();

        for (int i = 0; i < fallbackCount; i++)
        {
            var text = (await SafeGetElementTextAsync(fallbackElements.Nth(i))).Trim();

            if (string.Equals(
                NormalizeForComparison(text),
                NormalizeForComparison(selectedAddress),
                StringComparison.OrdinalIgnoreCase))
            {
                await fallbackElements.Nth(i).ClickAsync();
                return true;
            }
        }

        return false;
    }

    private async Task<bool> ClickShowCollectionDatesAsync(IPage page)
    {
        // Strategy 1: Exact ARIA role match
        var exactButton = page.GetByRole(AriaRole.Button, new()
        {
            Name = "Show collection dates",
            Exact = true
        });
        if (await exactButton.CountAsync() > 0)
        {
            Console.WriteLine("[CLICK] Strategy 1: found 'Show collection dates' by ARIA role.");
            await exactButton.First.ClickAsync();
            return true;
        }

        // Strategy 2: Partial text match across ALL interactive elements
        // (covers input[type=submit], anchor tags, and non-semantic buttons).
        // Log every candidate element so Docker logs show exactly what was on the page.
        var allInteractive = page.Locator("button, input[type='button'], input[type='submit'], a[href], [role='button']");
        var count = await allInteractive.CountAsync();
        Console.WriteLine($"[CLICK] Strategy 2: scanning {count} interactive elements...");

        for (int i = 0; i < count; i++)
        {
            var el   = allInteractive.Nth(i);
            var text = (await SafeGetElementTextAsync(el)).Trim();

            if (!string.IsNullOrWhiteSpace(text))
                Console.WriteLine($"  [{i:D2}] {repr(text)}");

            if (string.IsNullOrWhiteSpace(text)) continue;

            // Broad match: "show" + ("collection" or "dates") — catches renamed buttons
            bool hasShow       = text.Contains("show",       StringComparison.OrdinalIgnoreCase);
            bool hasView       = text.Contains("view",       StringComparison.OrdinalIgnoreCase);
            bool hasCollection = text.Contains("collection", StringComparison.OrdinalIgnoreCase);
            bool hasDates      = text.Contains("date",       StringComparison.OrdinalIgnoreCase);
            bool hasCalendar   = text.Contains("calendar",   StringComparison.OrdinalIgnoreCase);

            if ((hasShow || hasView) && (hasCollection || hasDates || hasCalendar))
            {
                Console.WriteLine($"[CLICK] Strategy 2 match (show/view+collection/dates): {repr(text)}");
                await el.ClickAsync();
                return true;
            }

            // Exact single-word variants sometimes used in Bradford's form
            if (string.Equals(text, "View dates", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Show dates", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Collection dates", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Check dates", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Continue", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[CLICK] Strategy 2 exact variant match: {repr(text)}");
                await el.ClickAsync();
                return true;
            }
        }

        // Strategy 3: CSS selector fallback — any submit-type input on the page
        // after address selection (Bradford's form typically has only one submit at this stage)
        var submitInputs = page.Locator("input[type='submit']");
        var submitCount  = await submitInputs.CountAsync();

        for (int i = 0; i < submitCount; i++)
        {
            var val = await submitInputs.Nth(i).GetAttributeAsync("value") ?? "";
            if (!val.Equals("Find address", StringComparison.OrdinalIgnoreCase) &&
                !val.Equals("Search", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[CLICK] Strategy 3: clicking submit input value={repr(val)}");
                await submitInputs.Nth(i).ClickAsync();
                return true;
            }
        }

        // Strategy 4: Last resort — click ANY visible button that isn't a known noise element
        // (privacy, accessibility, navigation, "Find address", "Search again").
        // After address selection, Bradford's form should have at most one "action" button.
        var noiseTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Find address", "Search", "Search again", "Search for another address",
            "Privacy notice", "View our privacy notice", "How we use your information",
            "Accessibility", "Cookies", "A to Z", "Close", "Save", "Back",
            "Print/save collection dates", "Home"
        };

        var allButtons = page.Locator("button, input[type='submit'], input[type='button']");
        var allBtnCount = await allButtons.CountAsync();

        for (int i = 0; i < allBtnCount; i++)
        {
            var el      = allButtons.Nth(i);
            var btnText = (await SafeGetElementTextAsync(el)).Trim();
            if (string.IsNullOrWhiteSpace(btnText)) continue;
            if (noiseTexts.Contains(btnText)) continue;

            Console.WriteLine($"[CLICK] Strategy 4 (catch-all): clicking button text={repr(btnText)}");
            await el.ClickAsync();
            return true;
        }

        Console.WriteLine("[CLICK] All 4 strategies exhausted — no suitable button found.");
        return false;
    }

    private static string repr(string s) => $"'{s}'";

    /// <summary>
    /// Returns true if the page body already contains the bin-collection results section.
    /// Bradford's 2026 form sends the user directly to this page after clicking an address,
    /// with no intermediate "Show collection dates" button.
    /// </summary>
    private static bool LooksLikeResultsPage(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return false;

        // The results page always has both "General waste" and "Recycling waste" headings
        // AND some indicator that dates or a summary are present.
        bool hasGeneral    = bodyText.Contains("General waste",   StringComparison.OrdinalIgnoreCase);
        bool hasRecycling  = bodyText.Contains("Recycling waste", StringComparison.OrdinalIgnoreCase);
        bool hasSummary    = bodyText.Contains("Your next general", StringComparison.OrdinalIgnoreCase)
                          || bodyText.Contains("Collections are on", StringComparison.OrdinalIgnoreCase)
                          || bodyText.Contains("collection dates",   StringComparison.OrdinalIgnoreCase);

        return hasGeneral && hasRecycling && hasSummary;
    }


    private async Task<string> ExtractCollectionResultsAsync(IPage page)
{
    var bodyText = await SafeGetBodyTextAsync(page);

    if (string.IsNullOrWhiteSpace(bodyText))
        return "";

    var lines = bodyText
        .Replace("\r", "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Where(x => !IsIgnoredResultLine(x))
        .ToList();

    // ── Diagnostic: log all lines so we can see what the page actually shows ──
    Console.WriteLine($"===== COLLECTION RESULTS PAGE — {lines.Count} lines =====");
    for (int i = 0; i < lines.Count; i++)
        Console.WriteLine($"  [{i:D3}] {lines[i]}");
    Console.WriteLine("=====  END OF COLLECTION RESULTS PAGE  =====");

    var nextCollectionText = ExtractNextCollectionFromSummary(lines);

    var generalDates = ExtractDatesUnderExactHeading(
        lines,
        "General waste",
        new[] { "Recycling waste", "Garden waste", "Collection information", "Print/save collection dates" });

    var recyclingDates = ExtractDatesUnderExactHeading(
        lines,
        "Recycling waste",
        new[] { "Garden waste", "Collection information", "Print/save collection dates" });

    // Garden dates appear AFTER the Recycling waste section on Bradford's results page.
    // "Garden waste" occurs TWICE: once as a subscription-status blurb (before the tables)
    // and once as the actual dates table (after Recycling waste). We must find the second
    // occurrence by starting the search from just after the Recycling waste heading.
    int recyclingHeadingIdx = FindHeadingIndex(lines, "Recycling waste", exactOnly: true);
    if (recyclingHeadingIdx < 0)
        recyclingHeadingIdx = FindHeadingIndex(lines, "Recycling waste", exactOnly: false);

    var gardenDates = ExtractDatesUnderExactHeading(
        lines,
        "Garden waste",
        new[] { "Collection information", "Print/save collection dates" },
        startAt: recyclingHeadingIdx >= 0 ? recyclingHeadingIdx + 1 : 0);

    Console.WriteLine($"[BIN PARSE] generalDates={generalDates.Count}, recyclingDates={recyclingDates.Count}, gardenDates={gardenDates.Count}");

    var generalEligible = generalDates.Count > 0;

    if (!generalEligible)
    {
        // ── Raw-scan fallback: if structured extraction found nothing, try scanning
        // every line for anything that looks like a collection date regardless of heading.
        // This is a last-resort guard against heading renames on Bradford's site.
        var rawDates = lines.Where(LooksLikeCollectionDate).ToList();
        Console.WriteLine($"[BIN PARSE] Structured extraction found 0 general dates. Raw scan found {rawDates.Count} date-like lines:");
        rawDates.ForEach(d => Console.WriteLine($"  RAW: {d}"));

        if (rawDates.Count > 0)
        {
            // Try to extract and format dates from the raw lines
            var parsedRaw = rawDates
                .Select(d => TryParseCollectionDate(d) ?? ExtractDateFromMixedLine(d))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .OrderBy(x => x)
                .Take(6)
                .Select(FormatDate)
                .ToList();

            var rawSummary = new List<string> { "Upcoming bin collections:" };
            if (parsedRaw.Count > 0)
                rawSummary.AddRange(parsedRaw.Select(d => $"- {d}"));
            else
                rawSummary.AddRange(rawDates.Take(6).Select(d => $"- {d}"));  // absolute last resort

            rawSummary.Add("");
            rawSummary.Add("🔗 Full details: https://www.bradford.gov.uk/bins-and-recycling/bin-collection-dates/");
            return string.Join(Environment.NewLine, rawSummary);
        }

        // Do NOT say "not eligible" here — the true failure is that we could not
        // parse any dates from the page.  The address may well be eligible; we just
        // could not read the dates automatically.
        Console.WriteLine("[BIN PARSE] Zero dates extracted after all strategies — returning honest fallback.");
        return
            "We could not automatically read your bin collection dates this time. " +
            "Please check them directly on the Bradford Council website: " +
            "https://www.bradford.gov.uk/bins-and-recycling/bin-collection-dates/";
    }

    // Recycling is automatically eligible if general waste is eligible.
    var recyclingEligible = true;

    // Garden subscription must be detected from the sentence under "Garden waste"
    var gardenStatusText = ExtractGardenSubscriptionText(lines);

    var gardenSubscribed =
        gardenStatusText.Contains("currently subscribed", StringComparison.OrdinalIgnoreCase) &&
        !gardenStatusText.Contains("not currently subscribed", StringComparison.OrdinalIgnoreCase);

    var gardenNotSubscribed =
        gardenStatusText.Contains("not currently subscribed", StringComparison.OrdinalIgnoreCase);

    // Only show garden dates when the page says subscribed and dates exist
    var gardenEligible = gardenSubscribed && gardenDates.Count > 0;

    var summary = new List<string>();

    if (!string.IsNullOrWhiteSpace(nextCollectionText))
    {
        summary.Add(nextCollectionText);
        summary.Add("");
    }
    else
    {
        var allUpcoming = new List<(DateTime date, string type)>();
        AddParsedDates(allUpcoming, generalDates, "General waste");
        AddParsedDates(allUpcoming, recyclingDates, "Recycling waste");

        if (gardenEligible)
            AddParsedDates(allUpcoming, gardenDates, "Garden waste");

        var nextCollection = allUpcoming
            .OrderBy(x => x.date)
            .FirstOrDefault();

        if (nextCollection != default)
        {
            summary.Add($"Next collection: {nextCollection.type} on {FormatDate(nextCollection.date)}");
            summary.Add("");
        }
    }

    AppendTopThree(summary, "General waste", generalDates, false);
    summary.Add("");

    if (recyclingDates.Count > 0)
    {
        AppendTopThree(summary, "Recycling waste", recyclingDates, false);
    }
    else if (recyclingEligible)
    {
        summary.Add("Recycling waste:");
        summary.Add("- Eligible, but no collection dates could be extracted");
    }
    else
    {
        summary.Add("Recycling waste: Not eligible");
    }

    summary.Add("");

    summary.Add($"Garden waste subscription: {GetGardenSubscriptionLabel(gardenSubscribed, gardenNotSubscribed)}");
    summary.Add("");

    if (gardenEligible)
    {
        AppendTopThree(summary, "Garden waste", gardenDates, false);
    }
    else
    {
        summary.Add("Garden waste: Subscription required");
    }

    return string.Join(Environment.NewLine, summary).Trim();
}
private static string ExtractGardenSubscriptionText(List<string> lines)
{
    for (int i = 0; i < lines.Count; i++)
    {
        // Flexible: exact OR startsWith (handles "Garden waste collection" etc.)
        if (lines[i].Equals("Garden waste", StringComparison.OrdinalIgnoreCase) ||
            (lines[i].StartsWith("Garden waste", StringComparison.OrdinalIgnoreCase) &&
             !lines[i].Contains("subscription", StringComparison.OrdinalIgnoreCase)))
        {
            var collected = new List<string>();

            for (int j = i + 1; j < lines.Count; j++)
            {
                var line = lines[j];

                if (line.Equals("General waste", StringComparison.OrdinalIgnoreCase) ||
                    line.Equals("Recycling waste", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Garden waste (subscription only)", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                collected.Add(line);
            }

            return string.Join(" ", collected);
        }
    }

    return "";
}

private static string GetGardenSubscriptionLabel(bool subscribed, bool notSubscribed)
{
    if (subscribed)
        return "Subscribed";

    if (notSubscribed)
        return "Not subscribed";

    return "Unknown";
}
    private static string ExtractNextCollectionFromSummary(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("Your next general/recycling collections are", StringComparison.OrdinalIgnoreCase))
            {
                for (int j = i + 1; j < Math.Min(i + 6, lines.Count); j++)
                {
                    var line = lines[j];

                    if (line.Contains("Recycling waste", StringComparison.OrdinalIgnoreCase))
                    {
                        var date = ExtractDateFromMixedLine(line);
                        if (date.HasValue)
                            return $"Next collection: Recycling waste on {FormatDate(date.Value)}";
                    }

                    if (line.Contains("General waste", StringComparison.OrdinalIgnoreCase))
                    {
                        var date = ExtractDateFromMixedLine(line);
                        if (date.HasValue)
                            return $"Next collection: General waste on {FormatDate(date.Value)}";
                    }
                }
            }
        }

        return "";
    }

    /// <summary>
    /// Slides a word-window across the line and tries to parse a date from each sub-string.
    /// Tries windows of 4 words first (e.g. "Mon 28 Apr 2025", "Monday 28 April 2025"),
    /// then 3 words (e.g. "28 Apr 2025", "28 April 2025"),
    /// then 2 words as a last resort (e.g. "28/04/2025" counts as one token but some
    /// formats need two).
    /// </summary>
    private static DateTime? ExtractDateFromMixedLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Try windows of decreasing size: 4 → 3 → 2
        foreach (var windowSize in new[] { 4, 3, 2 })
        {
            for (int i = 0; i <= words.Length - windowSize; i++)
            {
                var candidate = string.Join(" ", words.Skip(i).Take(windowSize));
                var parsed = TryParseCollectionDate(candidate);
                if (parsed.HasValue)
                    return parsed;
            }
        }

        // Single-token fallback: handles "28/04/2025" or "2025-04-28"
        foreach (var word in words)
        {
            var parsed = TryParseCollectionDate(word);
            if (parsed.HasValue)
                return parsed;
        }

        return null;
    }

    /// <summary>
    /// Extracts date lines that appear between a section heading and the next section heading.
    ///
    /// Uses a two-pass strategy:
    ///   1. Exact case-insensitive match (original behaviour — fastest, most precise).
    ///   2. StartsWith / Contains match — catches renamed headings like "General Waste collection"
    ///      or headings that gained/lost trailing punctuation after a site update.
    /// </summary>
    private static List<string> ExtractDatesUnderExactHeading(
        List<string> lines,
        string heading,
        string[] endHeadings,
        int startAt = 0)
    {
        // Pass 1: exact case-insensitive match
        int startIndex = FindHeadingIndex(lines, heading, exactOnly: true, startAt: startAt);

        // Pass 2: relaxed StartsWith/Contains match
        if (startIndex < 0)
            startIndex = FindHeadingIndex(lines, heading, exactOnly: false, startAt: startAt);

        if (startIndex < 0)
            return new List<string>();

        var endIndex = lines.Count;
        for (int i = startIndex + 1; i < lines.Count; i++)
        {
            bool isEnd =
                endHeadings.Any(h => string.Equals(lines[i].Trim(), h, StringComparison.OrdinalIgnoreCase)) ||
                endHeadings.Any(h => lines[i].Trim().StartsWith(h, StringComparison.OrdinalIgnoreCase));

            if (isEnd)
            {
                endIndex = i;
                break;
            }
        }

        return lines
            .Skip(startIndex + 1)
            .Take(endIndex - startIndex - 1)
            .Where(LooksLikeCollectionDate)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int FindHeadingIndex(List<string> lines, string heading, bool exactOnly, int startAt = 0)
    {
        for (int i = startAt; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (exactOnly)
            {
                if (string.Equals(line, heading, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            else
            {
                // Relaxed: the line starts with the heading keyword
                if (line.StartsWith(heading, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }
        return -1;
    }

    private static void AppendTopThree(List<string> summary, string label, List<string> dates, bool notEligible)
    {
        if (notEligible || dates.Count == 0)
        {
            summary.Add($"{label}: Not eligible");
            return;
        }

        summary.Add($"{label}:");

        foreach (var date in GetTopThreeDates(dates))
        {
            summary.Add($"- {date}");
        }
    }

    private static List<string> GetTopThreeDates(List<string> rawDates)
    {
        return rawDates
            // Try exact parse first; fall back to sliding-window extraction
            .Select(d => TryParseCollectionDate(d) ?? ExtractDateFromMixedLine(d))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .OrderBy(x => x)
            .Take(3)
            .Select(FormatDate)
            .ToList();
    }

    private static void AddParsedDates(List<(DateTime date, string type)> all, List<string> rawDates, string type)
    {
        foreach (var raw in rawDates)
        {
            var parsed = TryParseCollectionDate(raw) ?? ExtractDateFromMixedLine(raw);
            if (parsed.HasValue)
                all.Add((parsed.Value, type));
        }
    }

    private static DateTime? TryParseCollectionDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var input = value.Trim();

        // ── Ordered by most-likely format for Bradford Council's website ──────────
        // Bradford/UK government sites historically output dates like:
        //   "Mon 28 Apr 2025"  (abbreviated day, dd, abbreviated month, yyyy)
        //   "Monday 28 April 2025" (full day name)
        //   "Mon Apr 28 2025"  (old format — abbreviated day, abbreviated month, dd, yyyy)
        //   "28 April 2025"    (no day name)
        //   "28 Apr 2025"
        //   "28/04/2025"
        var formats = new[]
        {
            // UK day-first with abbreviated day name (most common on Bradford's site)
            "ddd dd MMM yyyy",
            "ddd d MMM yyyy",

            // UK day-first with full day name
            "dddd dd MMMM yyyy",
            "dddd d MMMM yyyy",

            // US month-first with abbreviated day name (old format still in logs)
            "ddd MMM dd yyyy",
            "ddd MMM d yyyy",

            // No day name, abbreviated month
            "dd MMM yyyy",
            "d MMM yyyy",

            // No day name, full month
            "dd MMMM yyyy",
            "d MMMM yyyy",

            // Numeric slash
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd/MM/yy",

            // Numeric dash
            "yyyy-MM-dd",
        };

        if (DateTime.TryParseExact(
            input,
            formats,
            CultureInfo.GetCultureInfo("en-GB"),
            DateTimeStyles.None,
            out var parsed))
        {
            return parsed;
        }

        // ── Loose fallback: let the runtime try its own UK-culture parsing ────────
        // Catches formats we didn't enumerate (e.g. "28th April 2025" after stripping suffix)
        var stripped = Regex.Replace(input, @"(\d+)(st|nd|rd|th)", "$1", RegexOptions.IgnoreCase);
        if (!string.Equals(stripped, input) &&
            DateTime.TryParse(stripped, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.None, out var parsed2))
        {
            return parsed2;
        }

        return null;
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns true if a line plausibly contains a collection date.
    /// Uses both exact parsing AND a lightweight regex pre-filter so that
    /// lines like "Mon 28 Apr 2025 (Bank Holiday – no change)" are still caught.
    /// </summary>
    private static bool LooksLikeCollectionDate(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;

        // Fast path: exact parse succeeds
        if (TryParseCollectionDate(line).HasValue) return true;

        // Slow path: line contains a 4-digit year and a known month name
        if (!Regex.IsMatch(line, @"\b20\d{2}\b")) return false;

        var months = new[]
        {
            "january","february","march","april","may","june",
            "july","august","september","october","november","december",
            "jan","feb","mar","apr","jun","jul","aug","sep","oct","nov","dec"
        };
        var lower = line.ToLowerInvariant();
        if (!months.Any(m => lower.Contains(m))) return false;

        // Must also contain a 1-2 digit day number
        return Regex.IsMatch(line, @"\b\d{1,2}\b");
    }

    private static bool IsIgnoredButton(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var ignored = new[]
        {
            "Find address",
            "Find out more",
            "Privacy notice",
            "How we use your information",
            "Contact us online",
            "Cookies",
            "Accessibility",
            "A to Z",
            "Close",
            "Show collection dates",
            "Search again",
            "Search for another address",
            "View our Privacy notice",
            "Save"
        };

        return ignored.Any(x => string.Equals(text.Trim(), x, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeAddressOption(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("BD", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ROAD", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("STREET", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("LANE", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("AVENUE", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("HOUSE", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("FLOOR", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("BRADFORD", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredResultLine(string text)
    {
        var ignoredFragments = new[]
        {
            "Privacy notice",
            "How we use your information",
            "Contact us online",
            "Find out more",
            "Cookies",
            "Accessibility",
            "A to Z",
            "Search again",
            "Search for another address",
            "Show collection dates",
            "View our Privacy notice",
            "Bradford Council sends regular bulletins",
            "You can opt in to receive relevant information",
            "For security purposes this form will time out after 20 minutes of inactivity",
            "Save address",
            "Print/save collection dates",
            "Print/save with images",
            "Print/save without images",
            "For more all information regarding garden waste collections visit our garden waste collections webpage",
            "Please note that this save feature will save a cookie",
            "If you wish to save this address select the Save button below"
        };

        return ignoredFragments.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeForComparison(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value
            .Replace(" ,", ",")
            .Replace(", ", ",")
            .Replace("  ", " ")
            .Trim()
            .ToUpperInvariant();
    }

    private static async Task<string> SafeGetBodyTextAsync(IPage page)
    {
        try
        {
            return await page.Locator("body").InnerTextAsync();
        }
        catch
        {
            return "";
        }
    }

    private static async Task<string> SafeGetElementTextAsync(ILocator locator)
    {
        try
        {
            var text = await locator.InnerTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }
        catch
        {
        }

        try
        {
            var value = await locator.GetAttributeAsync("value");
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        catch
        {
        }

        try
        {
            var ariaLabel = await locator.GetAttributeAsync("aria-label");
            if (!string.IsNullOrWhiteSpace(ariaLabel))
                return ariaLabel.Trim();
        }
        catch
        {
        }

        return "";
    }
}