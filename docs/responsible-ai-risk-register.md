# Responsible AI & Governance Risk Register

> This document is a retrospective portfolio governance review of a university client-project prototype. It is not an ISO certification, legal compliance assessment, Council production approval or production-readiness assessment.

**Review date:** October 2026

**System:** Citizen Support AI Agent, a prototype built for Bradford Council as a final-year client project (University of Bradford, 2025–26)

---

## Scope

**In scope**

- This repository at commit `492d7fa`. It is a portfolio copy, uploaded in one commit in May 2026 after the project ended.
- The team's code archive of 16 April 2026, used for comparison only (not published).
- Team project documents (not published): the final report and its test-output screenshots, the closure report, the team's ROAM risk analysis, a data and AI ethics self-assessment, testing and evaluation documents, the product backlog and the client exhibition feedback.

**Out of scope**

- The AWS EC2 environment used for the demonstration. Its network and firewall settings are not in the evidence.
- Re-running the test suites. No tests were run for this review. Every result quoted here comes from the team's own runs.
- Any Bradford Council system, policy or approval. The prototype used public Council web content and dummy data, and it was not adopted as a live Council service.

## Project context

- **What it is.** A chatbot that answers questions about Council services from the Council's public web pages and guides users through tasks such as finding their bin collection day.
- **How it is built.**
  - ASP.NET Core 10 orchestrator with rule-based routing and guided workflows.
  - Python FastAPI services: retrieval-augmented generation with LangChain, FAISS and GPT-4o-mini, and a local sentence-transformers embedding model.
  - Playwright to read the Council's bin-collection page.
  - Docker, Nginx and AWS EC2 for the project demonstration.
- **Who built it.** A team of three students. The prototype was demonstrated to the client in April 2026.
- **Why governance matters here.** Residents contact councils about homelessness, debt, benefits and safety. A wrong or missing answer on those topics can cause real harm.

## Assessment approach

1. **Evidence audit.** Each governance-related claim was traced to code, configuration, tests or documents and given a status label (definitions below). Code behaviour was established by reading the code, not by running it.
2. **Risk identification.** Only risks with evidence and real user impact are included. Others were considered and recorded with a reason for leaving them out.
3. **Retrospective scoring.** The method is in the next section.
4. **Framework mapping.** Frameworks are used only to organise the risks. No claim of compliance with any of them is made.

## Evidence-status definitions

| Label | Meaning |
|---|---|
| IMPLEMENTED | The behaviour or control is visible in the code or configuration. |
| TESTED | An actual test result supports the behaviour. |
| TEST EXISTS | A test exists, but no passing result was verified. |
| CONSIDERED | Project documents show the issue was considered; implementation is not established. |
| DOCUMENTED LIMITATION | The project explicitly records the weakness or boundary. |
| FUTURE IMPROVEMENT | Proposed, not implemented. |
| UNKNOWN | The evidence is insufficient. |
| NO VERIFIED EVIDENCE FOUND | No supporting file was located. |

## Risk-rating methodology

**Retrospective portfolio assessment — October 2026.** The original project did not score its risks. Its ROAM analysis classified risks as Resolved, Owned, Accepted or Mitigated. The scores below were added in October 2026 to structure this review.

**Scales**

- **Likelihood:** 1 Rare, 2 Unlikely, 3 Possible, 4 Likely, 5 Very likely.
- **Impact:** 1 Minimal, 2 Low, 3 Moderate, 4 Major, 5 Severe.
- **Score:** likelihood × impact. 1–4 Low, 5–9 Medium, 10–16 High, 17–25 Critical.

**Assumptions**

- Each risk is scored as if the prototype, as built, were used by members of the public. This is a judgement about the design, not a record of harm that occurred.
- **Inherent** means without the listed controls. **Residual** means with the controls as evidenced.

## Risk register

### Summary

| ID | Risk | Inherent | Residual | Key open issue |
|---|---|---|---|---|
| R1 | Crisis (self-harm/suicide) disclosure missed | 20 Critical | 10 High | Keyword-based guard with no automated test |
| R2 | Urgent housing / domestic-abuse disclosure misrouted | 16 High | 12 High | Test failures reported; no safeguarding check inside guided forms |
| R3 | Answers not supported by Council content | 16 High | 12 High | Faithfulness never measured; prompt discourages abstaining |
| R4 | Outdated or unverified service information | 12 High | 9 Medium | No refresh process; hard-coded figures and phone numbers |
| R5 | Personal data collected beyond need or sent to the AI provider | 16 High | 12 High | NI number and date of birth not masked; no DPIA or privacy notice |
| R6 | Logs that keep personal data and lose audit detail | 12 High | 9 Medium | Masking gaps; timestamps overwritten by masking; no retention period |
| R7 | Simulated outcomes presented as real | 12 High | 9 Medium | Mock booking says "booked" |
| R8 | Public endpoints without authentication or per-client limits | 16 High | 12 High | Security review needed before any public use |
| R9 | Evaluation results over-read | 16 High | 9 Medium | 85% figure measures routing, not answer accuracy |

### R1 — Crisis disclosure missed or mishandled

- **Category:** Safeguarding
- **Failure scenario:** A resident describes suicidal thoughts or self-harm in words outside the keyword lists, for example indirect language, a paraphrase or a misspelling. The message then goes to normal service routing and gets a service answer or a clarifying question instead of crisis signposting.
- **Affected party:** Residents in crisis.
- **Potential harm:** Delay in reaching urgent help, and distress.
- **Likelihood / Impact / Inherent score:** 4 / 5 / **20 Critical**. Without a dedicated check, such a message is routed like any other query.
- **Existing control:**
  - A deterministic crisis check runs first, before small talk, workflows and retrieval.
  - It returns a fixed reply pointing to emergency services, NHS 111 (mental-health option) and Samaritans (116 123). The language model is not involved.
  - A separate emotional-distress check exists, but only inside appointment flows.
- **Control status:**
  - Crisis check: **IMPLEMENTED**. It is also present in the team's 16 April 2026 archive.
  - Automated test for the crisis check: **NO VERIFIED EVIDENCE FOUND**. No test in the xUnit or Python suites exercises it.
  - Emotional-distress check: IMPLEMENTED in this repository. It is not in the 16 April archive.
- **Evidence:** `Services/ChatOrchestrator.cs` — `HandleChatAsync` (crisis check at the start), `IsCrisisIntent`, `IsEmotionalDistressStatement`.
- **Residual risk:** 2 / 5 / **10 High**. Explicit wording is caught reliably. Indirect wording is not. No test protects the check from regressions. No record shows the wording or helpline details were reviewed by a safeguarding professional.
- **Recommended next action:**
  - Add a crisis regression set covering explicit, paraphrased and misspelled messages, and messages sent mid-workflow.
  - Measure the miss rate.
  - Consider a second-stage classifier, keeping the keyword check as a floor.
  - Have the wording reviewed and record a review date for the helpline details.
- **Accountability / owner:** No named owner was recorded in the prototype; the student team was responsible. *Proposed for production:* a Council safeguarding lead (content) and an engineering owner (check and tests).
- **Review trigger:** Any change to the phrase lists, the routing order or the workflows; any reported missed disclosure; any change to helpline details.
- **Framework reference:** NIST AI RMF — MAP / MEASURE / MANAGE ("safe"). ISO/IEC 23894 — risk treatment. ISO/IEC 42001 — AI system impact assessment concept.
- **Source:** `Services/ChatOrchestrator.cs`; `Tests/` (no matching test).

### R2 — Urgent housing or domestic-abuse disclosure misrouted

- **Category:** Safeguarding
- **Failure scenario:** A resident says they have nowhere to sleep tonight or are not safe at home. Their wording may be outside the trigger lists, or they may be part-way through a guided form or an appointment booking. The message is then treated as routine, or recorded as a form or booking answer, and the urgent signposting is not shown.
- **Affected party:** Residents facing homelessness or domestic abuse.
- **Potential harm:** Delayed access to emergency accommodation or safety support.
- **Likelihood / Impact / Inherent score:** 4 / 4 / **16 High**.
- **Existing control:**
  - A housing navigator gives fixed replies for rough sleeping, eviction risk and domestic abuse, including 999, a national domestic-abuse helpline and a 24-hour homelessness line. It runs before retrieval.
  - Urgent-housing messages can leave an appointment flow.
- **Control status:**
  - Housing navigator: **IMPLEMENTED**.
  - Tests: **TEST EXISTS** (`07_Safeguarding`, and 5 safeguarding cases in `Tests/eval/test_cases.json`).
  - Results: the team's xUnit run reported 23 failures out of 384, including urgent-housing and domestic-abuse cases. Which tests failed is not recorded.
  - In the team's 160-query routing run, 11 of 20 Housing queries were routed correctly.
  - **NO VERIFIED EVIDENCE FOUND** of a safeguarding check before a guided-form answer is recorded.
  - Reading the code suggests "not safe at home" would not escape an appointment flow. This has not been run.
- **Evidence:**
  - `Services/HousingNavigatorService.cs`
  - `Services/ChatOrchestrator.cs` — `IsHousingUrgentIntent`, `IsUnrelatedToAppointmentFlow`, the form-flow block
  - `Tests/BradfordChatbot.Tests/07_Safeguarding/SafeguardingTests.cs`
  - Team final report (test outputs)
- **Residual risk:** 3 / 4 / **12 High**.
- **Recommended next action:**
  - Run one safeguarding check before every workflow step.
  - Align the trigger lists across the router, the navigator and the escape checks.
  - Fix and publish the failing safeguarding tests.
  - Verify every phone number with the Council and record a review date.
- **Accountability / owner:** No named owner was recorded. *Proposed:* the Council's housing-options or safeguarding lead with an engineering owner.
- **Review trigger:** A change to housing or domestic-abuse services or numbers; a new workflow; a failing safeguarding test.
- **Framework reference:** NIST AI RMF — MAP / MEASURE / MANAGE. ISO/IEC 23894. ISO/IEC 42001 — impact assessment concept.
- **Source:** As listed under Evidence.

### R3 — Answers not supported by Council content

- **Category:** Hallucination / retrieval quality
- **Failure scenario:**
  - When retrieval is weak but a service keyword was matched, the orchestrator still asks the agent to answer. A code comment says the agent may use "its own training knowledge".
  - The generation prompt also tells the model not to use its fallback reply "when an approximate or partial answer can be given".
  - Together these can produce a plausible but unsupported answer about eligibility, deadlines or amounts.
- **Affected party:** Residents relying on the guidance; the Council's reputation.
- **Potential harm:** Missed entitlements, arrears or complaints.
- **Likelihood / Impact / Inherent score:** 4 / 4 / **16 High**.
- **Existing control:**
  - Retrieval over Council-derived content, scoped by service.
  - Prompt rules: "Use only facts that appear in the information below" and "Do not invent prices, dates, deadlines, phone numbers, email addresses, or eligibility rules".
  - Temperature 0.1.
  - Hedged or empty answers are replaced with a clarifying question.
  - Next-step links to official pages.
- **Control status:**
  - Controls above: **IMPLEMENTED**.
  - Answer quality: **TESTED** in the team's 45-query run. Response quality passed 37/45; overall 35/45 (77.78%), below the 85% target.
  - Faithfulness to sources: never measured. This is a **DOCUMENTED LIMITATION** (`Tests/COVERAGE_GAPS.md` §1.2).
  - The team's ROAM analysis rated this risk "Mitigated". This review rates it partly mitigated.
- **Evidence:**
  - `LangChainService/tools.py` — `generate_answer_with_llm`, `is_weak_answer`
  - `Services/ChatOrchestrator.cs` — the weak-retrieval branch and `IsGenericOrWeakReply`
  - `Tests/COVERAGE_GAPS.md`
- **Residual risk:** 3 / 4 / **12 High**.
- **Recommended next action:**
  - Allow the model to say it does not know when retrieval is weak.
  - Show the source link with every generated answer.
  - Add a faithfulness evaluation: a golden set checked against sources, or a model-graded check calibrated by people.
  - Use per-service thresholds as a release gate.
- **Accountability / owner:** No named owner was recorded. *Proposed:* an engineering owner with service content owners.
- **Review trigger:** Any change to the model, prompt or retrieval index; a new service area.
- **Framework reference:** NIST AI RMF — MEASURE ("valid and reliable") / MANAGE. OWASP Top 10 for LLM Applications (2025) — Misinformation. ISO/IEC 42001 — verification and validation concept.
- **Source:** As listed under Evidence.

### R4 — Outdated or unverified service information

- **Category:** Source freshness
- **Failure scenario:** Content drifts from current Council information. Contributing factors:
  - The knowledge base comes from a one-off scrape with no retrieval date stored.
  - The 118 hand-written FAQs have no review dates.
  - Some replies contain hard-coded details: a council-tax band table labelled 2024/25, phone numbers and addresses, and school results from mock data that the reply does not label as mock.
- **Affected party:** Residents; the Council.
- **Potential harm:** Wrong amounts, dead contact routes or wrong school details. Severe if a helpline number were wrong.
- **Likelihood / Impact / Inherent score:** 4 / 3 / **12 High**.
- **Existing control:**
  - The scraper accepts only Council domains.
  - Answers link to official pages.
  - Changing the FAQ file triggers re-embedding, using a SHA-256 fingerprint.
  - Location results say when distances are approximate.
- **Control status:**
  - Controls above: **IMPLEMENTED**.
  - Refresh process: **NO VERIFIED EVIDENCE FOUND**.
  - Keeping information current was named as a key risk in project documents: **CONSIDERED**.
  - Accuracy of the hard-coded figures and numbers: **UNKNOWN** (not checked in this review).
- **Evidence:**
  - `LangChainService/scrape_bradford_targeted.py` — `ALLOWED_DOMAINS`, `is_allowed_url`, page record fields
  - `Program.cs` — `FingerprintFaqs`
  - `Services/ChatOrchestrator.cs` — `BuildCouncilTaxAmountReply`
  - `Services/HousingNavigatorService.cs`
  - `Services/SchoolFinderService.cs` (marked MOCK in the code)
- **Residual risk:** 3 / 3 / **9 Medium**.
- **Recommended next action:**
  - Store the source URL and retrieval date for each chunk, and show "last checked".
  - Move hard-coded figures and numbers into one reviewed content file with owners and review dates.
  - Label mock data in replies.
- **Accountability / owner:** No named owner was recorded. *Proposed:* Council content owners for each service.
- **Review trigger:** Council website changes; annual council-tax setting; a scheduled content review.
- **Framework reference:** NIST AI RMF — MAP / MANAGE. ISO/IEC 42001 — data quality and provenance concepts. ISO/IEC 23894 — monitoring and review.
- **Source:** As listed under Evidence.

### R5 — Personal data collected beyond need or sent to the AI provider

- **Category:** Privacy
- **Failure scenario:**
  - Guided forms ask for a National Insurance number, date of birth, address, income and savings. The appointment flow asks for name, phone number and email.
  - Masking covers postcodes, emails, phone numbers and "my name is…" statements only.
  - Reading the code (not running it) suggests a completed form summary stays in session memory. On the next question that reaches retrieval, it is sent to the AI provider as conversation history.
  - The current message is also sent to the provider without masking.
- **Affected party:** Residents.
- **Potential harm:** Identifiers and financial details disclosed to a third party without a privacy notice or impact assessment; loss of trust.
- **Likelihood / Impact / Inherent score:** 4 / 4 / **16 High**.
- **Existing control:**
  - Masking in session memory.
  - Sessions held in memory only, expiring after 30 minutes and capped at 10 turns.
  - Embeddings computed locally, so query text is not sent to an embeddings API.
  - Interface notices: "do not share personal or sensitive information".
  - Forms are drafts and nothing is submitted.
- **Control status:**
  - Controls above: **IMPLEMENTED**.
  - Memory masking: **TEST EXISTS** (`03_GDPR`). No per-test result was available.
  - Masking of NI numbers, dates of birth and names: **NO VERIFIED EVIDENCE FOUND**.
  - No DPIA was carried out and no privacy notice was provided: **DOCUMENTED LIMITATION**. The client's exhibition feedback asked for a privacy notice.
  - Session deletion: **FUTURE IMPROVEMENT** (`Tests/COVERAGE_GAPS.md` §5.2).
- **Evidence:**
  - `Services/FormFlowService.cs` — form fields
  - `Services/ConversationMemory.cs` — `SanitizeForMemory`
  - `Services/ChatOrchestrator.cs` — `SaveConversation`, history passed to the agent
  - `LangChainService/tools.py` — history block in the prompt
  - `EmbeddingService/app.py`
  - `wwwroot/index.html`
  - `Tests/BradfordChatbot.Tests/03_GDPR/GdprPrivacyTests.cs`
- **Residual risk:** 3 / 4 / **12 High**.
- **Recommended next action:**
  - Stop collecting identifiers in chat and link to the Council's own authenticated forms, or keep form data out of model context.
  - Redact identifiers before any model call.
  - Carry out a DPIA, publish a privacy notice and set a retention schedule.
  - Review the provider's data-processing terms.
- **Accountability / owner:** No named owner was recorded. *Proposed:* the Council's data protection officer with the service and engineering owners.
- **Review trigger:** A new data field; a new provider or model; any change to what is sent to the model.
- **Framework reference:**
  - UK data-protection principles: data minimisation, transparency, security. High-level reference only; no legal conclusion.
  - NIST AI RMF — MAP / MEASURE ("privacy-enhanced").
  - OWASP Top 10 for LLM Applications (2025) — Sensitive Information Disclosure.
- **Source:** As listed under Evidence.

### R6 — Logs that keep personal data and lose audit detail

- **Category:** Logging / privacy / accountability
- **Failure scenario:**
  - Interaction logs store user messages. Masking misses NI numbers, dates of birth, names and free-text details.
  - The broad address-masking pattern also matches ISO timestamps and overwrites them, so records cannot be reliably ordered when investigating an incident.
  - Debug output prints the full text of the Council's bin-collection page, including address lists, to container logs.
  - No retention period is defined.
- **Affected party:** Residents (privacy); the team or Council (ability to investigate).
- **Potential harm:** Personal data kept indefinitely; incidents that cannot be reconstructed.
- **Likelihood / Impact / Inherent score:** 4 / 3 / **12 High**.
- **Existing control:** Two layers of log masking (controller and logging service) for postcodes, emails, phone numbers and address-like strings. Logs are written to a Docker volume.
- **Control status:**
  - Masking: **IMPLEMENTED**.
  - Checked against the team's archived development logs (2,050 records, February–April 2026) in October 2026:
    - The first 383 records predate the address masking. A few of them contain unmasked postcode-format strings.
    - In the remaining 1,667 records, masking replaced every timestamp with "[ADDRESS]".
  - Retention period: **NO VERIFIED EVIDENCE FOUND**.
  - A project document describes this control as "encryption". It is masking, not encryption.
- **Evidence:**
  - `Controllers/ChatController.cs` — `SanitizeForLog`
  - `Services/LoggingService.cs` — `SanitizeString`
  - `Services/PlaywrightService.cs` — console page dumps
  - Archived logs (team archive, not published)
- **Residual risk:** 3 / 3 / **9 Medium**.
- **Recommended next action:**
  - Write timestamps outside the masked fields.
  - Log an allow-list of fields (service, intent, score) instead of raw text.
  - Add NI-number and date patterns.
  - Remove the page dumps.
  - Set retention and deletion, and restrict access to logs.
- **Accountability / owner:** No named owner was recorded. *Proposed:* an engineering owner with the data protection officer.
- **Review trigger:** A change to the log schema; any incident.
- **Framework reference:** UK data-protection principles: storage limitation and security (high level). NIST AI RMF — GOVERN / MANAGE ("accountable and transparent"). ISO/IEC 42001 — event logging concept.
- **Source:** As listed under Evidence.

### R7 — Simulated outcomes presented as real

- **Category:** User transparency
- **Failure scenario:**
  - The appointment flow replies "✅ Your appointment has been booked", gives a reference number and tells the user to call the Council's real number to cancel. The booking is a mock held in memory.
  - School-finder results come from mock data without a label in the reply.
- **Affected party:** Residents; Council contact staff.
- **Potential harm:** A resident believes an appointment exists and does not book a real one; staff receive calls about references that do not exist.
- **Likelihood / Impact / Inherent score:** 4 / 3 / **12 High**.
- **Existing control:**
  - The interface labels the system "AI Support Agent" and "Prototype".
  - Guided forms say "This is a draft — nothing has been submitted yet".
  - The mock services are marked MOCK in the code.
- **Control status:**
  - Interface labels and form wording: **IMPLEMENTED**.
  - Disclosure in the booking confirmation: **NO VERIFIED EVIDENCE FOUND**.
  - `AppointmentFlowTests` exists, but no test checks the disclosure wording.
- **Evidence:** `Services/AppointmentService.cs` — `ConfirmBooking`; `Services/FormFlowService.cs`; `Services/SchoolFinderService.cs`; `wwwroot/index.html`.
- **Residual risk:** 3 / 3 / **9 Medium**.
- **Recommended next action:** Label simulated actions in the reply itself ("Demo only — no appointment has been booked"). Switch mock features off in any public deployment. Add a test for the disclosure.
- **Accountability / owner:** No named owner was recorded. *Proposed:* the service owner.
- **Review trigger:** Any public deployment; integration with a real booking system.
- **Framework reference:** NIST AI RMF — GOVERN / MAP ("accountable and transparent"). ISO/IEC 42001 — information for interested parties concept.
- **Source:** As listed under Evidence.

### R8 — Public endpoints without authentication or per-client limits

- **Category:** Security / cost / availability
- **Failure scenario:**
  - None of the API endpoints requires authentication.
  - Throttling covers only part of the API and is not set per client, including on features that incur provider costs.
  - The network exposure of the internal services in the demonstration deployment was not reviewed or documented.
- **Affected party:** The project or Council (cost, availability); residents (availability, booking details).
- **Potential harm:** Cost abuse, denial of service, and access to functions or stored details without any identity check.
- **Likelihood / Impact / Inherent score:** 4 / 4 / **16 High**.
- **Existing control:**
  - HTTPS through Nginx.
  - The application port is not published, so traffic goes through Nginx.
  - A fixed-window rate limit on chat.
  - API keys read from environment variables. A pattern scan of the full commit history in October 2026 found no committed keys.
  - Size and length limits on voice input and output.
- **Control status:**
  - Controls above: **IMPLEMENTED**.
  - Load testing of the rate limit: **NO VERIFIED EVIDENCE FOUND** (`Tests/COVERAGE_GAPS.md` §5.1).
  - Authentication: the team's final report recommends adding it — **DOCUMENTED LIMITATION**.
  - Network exposure on EC2: **UNKNOWN**.
- **Evidence:**
  - `Program.cs` — rate limiter
  - `Controllers/` — no authorisation attributes
  - `Infrastructure/` — compose and Nginx files
- **Residual risk:** 3 / 4 / **12 High**.
- **Recommended next action:**
  - Per-client limits on every endpoint that costs money, plus provider spending caps.
  - Expose only the reverse proxy.
  - Authentication for anything account-linked.
  - A security review or penetration test, a secrets manager and pinned image versions.
- **Accountability / owner:** No named owner was recorded. *Proposed:* engineering and security owners.
- **Review trigger:** Any public deployment; a new endpoint; a change in provider pricing.
- **Framework reference:** OWASP Top 10 for LLM Applications (2025) — Unbounded Consumption. OWASP Top 10 (web) — broken access control and authentication failures. NIST AI RMF — MEASURE / MANAGE ("secure and resilient").
- **Source:** As listed under Evidence.

### R9 — Evaluation results over-read

- **Category:** Evaluation / accountability
- **Failure scenario:** The headline "about 85% accuracy" is read as answer accuracy or readiness. It actually measures this:
  - the share of 160 single-turn questions routed to the expected service,
  - in one run,
  - with one shared session across all tests.

  Other evidence is weaker:
  - The stricter 45-query run passed 35/45 (77.78%), below target.
  - The xUnit run had 23 failures out of 384.
  - An earlier archived run of the 160-query script scored 108/160 (67.5%). In that run, conversation state carried over in the shared test session and every Council Tax question was routed to the appointment flow.
  - No faithfulness, accessibility or real-user testing was done.
- **Affected party:** Council decision-makers; residents, if a deployment decision relied on it.
- **Potential harm:** Over-confident deployment; loss of credibility.
- **Likelihood / Impact / Inherent score:** 4 / 4 / **16 High**.
- **Existing control:**
  - Several layers of evaluation: the 45-query run (routing, response, link and suggestion checks), the 160-query routing run, 384 xUnit tests, and persona test scenarios.
  - The team's final report concludes the prototype is "a strong proof of concept, not a deployment-ready public service" and reports the shortfalls.
  - `Tests/COVERAGE_GAPS.md` lists what was not tested.
- **Control status:**
  - Run results: **TESTED** (team output screenshots and an archived results file).
  - Untested areas: **DOCUMENTED LIMITATION**.
  - Some planning documents claim "GDPR compliance" and "WCAG 2.1 AA". These are **CONSIDERED** only; no audit evidence exists.
  - Persona scenarios were designed, but no per-persona results were recorded.
- **Evidence:** `Tests/COVERAGE_GAPS.md`; `Tests/eval/`; team final report and archived 160-query script and results (not published).
- **Residual risk:** 3 / 3 / **9 Medium**.
- **Recommended next action:**
  - Always state what each metric measures.
  - Give every test its own session.
  - Repeat runs and report the spread.
  - Add faithfulness, safeguarding and accessibility gates.
  - Keep results files in the repository.
- **Accountability / owner:** The author owns portfolio claims about this project. *Proposed for production:* an evaluation owner.
- **Review trigger:** Any public claim about performance; any re-run.
- **Framework reference:** NIST AI RMF — MEASURE / GOVERN. ISO/IEC 23894 — risk evaluation. ISO/IEC 42001 — verification and validation concept.
- **Source:** As listed under Evidence.

### Considered but not prioritised

- **Prompt injection and excessive agency.** User and retrieved text both go into the prompt. However, the model only writes text: lookups and workflows are triggered by ordinary code, not by the model, and no secrets are in the prompt. The main impact would be misleading text, which R3 covers. Red-team testing was not done (`Tests/COVERAGE_GAPS.md` §5.3).
- **Untrusted retrieved content.** Sources are limited to Council domains.
- **Provider outage.** Deterministic flows keep working, and generated answers fail with an error message. There is no fallback provider.
- **Fairness and accessibility.** These are covered under R9 and the future production considerations. Persona scenarios exist, but there is no audit and no testing with users of assistive technology.

## Existing controls

Controls present in this repository:

1. A crisis check that runs first and returns a fixed signposting reply.
2. A housing and domestic-abuse navigator with fixed replies.
3. A refusal for unsafe or illegal requests, and handling for out-of-scope questions.
4. The language model only writes text; workflows and lookups are triggered by deterministic code.
5. Service-scoped retrieval with prompt-level grounding and filtering of weak answers.
6. A scraper that accepts only Council domains.
7. Masking of postcodes, emails and phone numbers in session memory and logs.
8. In-memory sessions with a 30-minute expiry.
9. Local embeddings.
10. Draft-only guided forms.
11. AI and prototype labels in the interface.
12. HTTPS through a reverse proxy.
13. A fixed-window rate limit on chat.
14. Secrets read from environment variables.
15. xUnit test categories for privacy (`03_GDPR`) and safeguarding (`07_Safeguarding`).

## Evidence summary

- **Verified implemented controls:** the 15 items above.
- **Tested:**
  - Team runs: 160-query routing (136/160); 45-query quality (35/45, below target); xUnit (360 of 384 passed, 23 failed, 1 skipped).
  - Log masking, checked against archived logs in this review.
- **Controls with tests but no verified per-test result:** memory masking (`03_GDPR`) and housing safeguarding routing (`07_Safeguarding`).
- **Considered but not implemented:**
  - GDPR compliance and WCAG 2.1 AA (backlog and planning documents).
  - Escalation to a person.
  - CI/CD (named in the ROAM analysis; no workflow exists).
- **Documented limitations:**
  - Faithfulness not measured.
  - No DPIA and no privacy notice.
  - No human oversight in this version.
  - No authentication.
  - Untested areas listed in `Tests/COVERAGE_GAPS.md`.
- **Unknown or missing evidence:**
  - EC2 network configuration.
  - Which tests failed.
  - Whether the demonstrated build matches this repository.
  - Whether hard-coded figures and phone numbers are accurate.
  - The provider's data-retention settings.
- **Future production requirements:** see the next two sections.

## Known limitations

- Prototype only.
- Public web content plus dummy data; no integration with Council systems.
- No authentication.
- Keyword-based safeguarding with no test for the crisis check.
- Faithfulness not measured.
- Simulated booking and mock school data.
- No retention schedule.
- No human hand-off.
- No accessibility audit.
- Single-run evaluation with tests sharing one session.

## Future production considerations

These are not claims about the university prototype.

1. **Safeguarding assurance.** Professionally reviewed content; a safeguarding check before every workflow step; a crisis and urgent-housing regression set with pass criteria; a route to a person.
2. **Privacy by design.** A DPIA and privacy notice; no identifiers collected in chat; redaction before model calls; reviewed provider terms; a retention and deletion schedule.
3. **Security hardening.** Authentication; per-client limits and spending caps; only the proxy exposed; a penetration test; secrets management.
4. **Content governance and change control.** Named content owners and review dates; retrieval dates shown; versioned prompts, models and index; release gates and rollback.
5. **Monitoring and evaluation.** Faithfulness, safeguarding and accessibility (WCAG 2.2 AA) tests with real users; production monitoring; an incident process with a way to switch the service off.

## Framework references

Frameworks are used here only to organise the review. No certification or compliance with any framework is claimed.

- **NIST AI Risk Management Framework 1.0:** GOVERN, MAP, MEASURE, MANAGE, and its trustworthiness characteristics.
- **ISO/IEC 23894:2023:** AI risk-management concepts (identification, analysis, evaluation, treatment, monitoring).
- **ISO/IEC 42001:2023:** AI management-system concepts (risk and impact assessment, data quality, logging, information for users, verification and validation).
- **OWASP Top 10 for LLM Applications (2025 edition)**, and the OWASP Top 10 for web applications.
- **UK data-protection principles:** high-level reference only, with no legal conclusion.
- **EU AI Act:** not applied. No legal classification of this system is made.

## Review information

- **Review type:** retrospective portfolio review of a university prototype.
- **Date:** October 2026.
- **Author:** Samik Hafeez.
- **Method note:** prepared with AI assistance for code search and document review. The author checked the findings before publication.
- **Team attribution:** this was a team project. Controls described here were built by the team. This document does not credit individual controls to anyone. The repository is a portfolio copy, so its commit history does not show who wrote what.
- **Next review:** before any reuse or redeployment of the design, or after any change to this repository.
