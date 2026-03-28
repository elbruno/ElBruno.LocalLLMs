# Prompt Distillation Benchmarks

> **Purpose:** Validate that Qwen2.5-0.5B correctly distills complex user prompts into single-sentence summaries for MCP tool routing.  
> **Author:** Tank (QA)  
> **Date:** 2026-03-29  
> **Status:** Test design — awaiting sample implementation for actual results

---

## 1. Reference Tool Set

All benchmarks assume the following 8 MCP tools are registered in `MCPToolRouter`:

| Tool ID | Tool Name | Description |
|---------|-----------|-------------|
| `weather` | Weather Lookup | Get current weather conditions and forecasts for a given location |
| `email` | Email Sender | Compose and send emails to specified recipients |
| `calendar` | Calendar Manager | Create, read, update, and delete calendar events and appointments |
| `file_search` | File Search | Search for files by name, content, or metadata across local storage |
| `calculator` | Calculator | Perform mathematical calculations, unit conversions, and formula evaluation |
| `web_search` | Web Search | Search the internet for information, articles, and current events |
| `database` | Database Query | Execute SQL queries against connected databases and return results |
| `code_exec` | Code Execution | Run code snippets in sandboxed environments and return output |

---

## 2. Test Prompt Collection

### 2.1 Simple Single-Intent (7 prompts)

These are clean, unambiguous prompts with a single clear tool target. Distillation should be near-passthrough.

| ID | Input Prompt | Expected Distilled Output | Primary Tool | Top-3 Tools | Notes |
|----|-------------|---------------------------|-------------|-------------|-------|
| S01 | "What's the weather in Seattle?" | "Get weather for Seattle." | `weather` | weather, web_search, calendar | Canonical simple case |
| S02 | "Send an email to john@example.com about the meeting" | "Send email to john@example.com about a meeting." | `email` | email, calendar, file_search | Clear email intent |
| S03 | "What is 42 * 17 + 3?" | "Calculate 42 * 17 + 3." | `calculator` | calculator, code_exec, web_search | Pure math |
| S04 | "Find all PDF files in my Documents folder" | "Search for PDF files in Documents folder." | `file_search` | file_search, database, web_search | File system operation |
| S05 | "Search the web for latest news on AI regulations" | "Search web for AI regulation news." | `web_search` | web_search, database, file_search | Explicit web search |
| S06 | "Run this Python script: print('hello world')" | "Execute Python code to print hello world." | `code_exec` | code_exec, calculator, file_search | Code execution |
| S07 | "Show me all appointments for next Tuesday" | "Get calendar appointments for next Tuesday." | `calendar` | calendar, database, file_search | Calendar read |

### 2.2 Complex Multi-Intent (7 prompts)

These prompts contain multiple actions. Distillation must identify the **primary** intent (first or most prominent action).

| ID | Input Prompt | Expected Distilled Output | Primary Tool | Top-3 Tools | Notes |
|----|-------------|---------------------------|-------------|-------------|-------|
| M01 | "Check the weather in Boston, then send an email to my team about whether we should have the outdoor event" | "Check weather in Boston for outdoor event planning." | `weather` | weather, email, calendar | Weather is the prerequisite |
| M02 | "Calculate my quarterly taxes, search for the latest tax brackets online, and email the results to my accountant" | "Calculate quarterly taxes using current tax brackets." | `calculator` | calculator, web_search, email | Calculation is the core task |
| M03 | "Find all the CSV files from last month's reports, query the database for sales figures, and generate a summary" | "Search for last month's CSV report files." | `file_search` | file_search, database, code_exec | File discovery first |
| M04 | "Look up the meeting time on my calendar, search for directions to the venue, and send the details to all attendees" | "Look up meeting time and venue from calendar." | `calendar` | calendar, web_search, email | Calendar is the anchor |
| M05 | "Run the test suite, check if there are any errors in the log files, and email the status report to the dev team" | "Execute test suite and check for errors." | `code_exec` | code_exec, file_search, email | Execution is primary |
| M06 | "Search online for the best pizza recipe, calculate ingredient amounts for 8 people, and add a cooking event to my calendar" | "Search web for pizza recipe for 8 people." | `web_search` | web_search, calculator, calendar | Web research first |
| M07 | "Query the user database for inactive accounts, find their associated files, and send warning emails" | "Query database for inactive user accounts." | `database` | database, file_search, email | DB query is the starting point |

### 2.3 Verbose / Chatty (7 prompts)

Long prompts with filler, pleasantries, or excessive context. Distillation must cut through the noise.

| ID | Input Prompt | Expected Distilled Output | Primary Tool | Top-3 Tools | Notes |
|----|-------------|---------------------------|-------------|-------------|-------|
| V01 | "Hey there! I was just wondering, and I know this might sound like a silly question, but could you possibly check what the weather is going to be like tomorrow in San Francisco? I'm planning a picnic and I really need to know if I should bring an umbrella or not. Thanks so much in advance!" | "Get tomorrow's weather forecast for San Francisco." | `weather` | weather, web_search, calendar | Buried intent in social padding |
| V02 | "So, okay, here's the thing — I've been going back and forth about this all morning and I finally decided I need to just do it. Can you help me write and send an email to my boss, Sarah, letting her know I'll be taking Friday off? I know it's last minute but it is what it is. Her address is sarah@company.com." | "Send email to sarah@company.com about taking Friday off." | `email` | email, calendar, file_search | Decision narrative before request |
| V03 | "I remember reading somewhere, maybe it was on Reddit or Hacker News, that there was this really interesting article about quantum computing breakthroughs in 2026. I think it was published in the last week or two. Could you try to find it for me? I'd really appreciate it if you could dig that up." | "Search web for recent quantum computing breakthroughs article." | `web_search` | web_search, file_search, database | Memory/source rambling |
| V04 | "Right, so I'm sitting here with a pile of receipts from my business trip last week — hotels, meals, Uber rides, the whole nine yards. I need to add up all the expenses. The amounts are: $189.50, $45.00, $67.30, $12.99, $234.00, $55.50, and $28.75. What's the total? And yeah, I know I should use a spreadsheet but I'm lazy." | "Calculate the sum of business trip expenses." | `calculator` | calculator, code_exec, file_search | Self-deprecating filler around math |
| V05 | "Okay so this is kind of urgent and I'm sorry to bother you but I really need to find a file. It's a PowerPoint presentation that I was working on last Thursday, or maybe Wednesday? It had 'Q3 Revenue' in the title, I think. Could be in my Downloads folder or maybe on the Desktop. I'm not sure. Please help!" | "Search for Q3 Revenue PowerPoint presentation file." | `file_search` | file_search, database, web_search | Uncertainty and urgency noise |
| V06 | "I was talking to my colleague Dave the other day and he mentioned this really cool trick where you can write a Python one-liner to sort a list. I want to try it out. Can you run `sorted([5, 3, 8, 1, 9, 2, 7])` and show me the result? I'm just learning Python so bear with me." | "Execute Python code to sort a list of numbers." | `code_exec` | code_exec, calculator, web_search | Learning context + social filler |
| V07 | "You know, I've been meaning to do this for weeks now and I keep putting it off, but I really need to check my schedule. Like, what do I have on my calendar for the rest of this week? I have a feeling I double-booked something because I was rushing when I scheduled those meetings. Can you pull up everything from today through Sunday?" | "Get all calendar events for the rest of this week." | `calendar` | calendar, file_search, email | Procrastination narrative |

### 2.4 Ambiguous (7 prompts)

Prompts where the intent could map to multiple tools. Distillation should pick the most likely interpretation.

| ID | Input Prompt | Expected Distilled Output | Primary Tool | Top-3 Tools | Notes |
|----|-------------|---------------------------|-------------|-------------|-------|
| A01 | "I need to look something up" | "Search for information." | `web_search` | web_search, file_search, database | Extremely vague — web search is safest default |
| A02 | "Help me with my report" | "Search for report files or data." | `file_search` | file_search, database, code_exec | "Report" could mean find, write, or generate |
| A03 | "What's happening on Friday?" | "Check calendar for Friday events." | `calendar` | calendar, web_search, email | Could be calendar or current events |
| A04 | "I need to crunch some numbers from the dataset" | "Perform calculations on dataset values." | `calculator` | calculator, database, code_exec | Numbers could mean math, SQL, or code |
| A05 | "Can you reach out to the team about this?" | "Send email to the team." | `email` | email, calendar, web_search | "Reach out" implies communication |
| A06 | "Process this data for me" | "Execute code to process data." | `code_exec` | code_exec, database, calculator | "Process" is generic — code is the flexible option |
| A07 | "I need information about the Johnson account" | "Query database for Johnson account information." | `database` | database, file_search, web_search | "Account" suggests structured data |

### 2.5 Edge Cases (8 prompts)

Boundary conditions: very short, empty-ish, nonsensical, adversarial, or no clear tool match.

| ID | Input Prompt | Expected Distilled Output | Primary Tool | Top-3 Tools | Notes |
|----|-------------|---------------------------|-------------|-------------|-------|
| E01 | "" | *(empty or pass-through)* | `none` | web_search, file_search, calculator | Empty input — system should handle gracefully |
| E02 | "hi" | "General greeting — no specific action." | `none` | web_search, email, calendar | No actionable intent |
| E03 | "asdfghjkl qwerty zxcvbnm" | "No recognizable request." | `none` | web_search, file_search, code_exec | Keyboard mash — should not crash |
| E04 | "?" | "Unclear request." | `none` | web_search, file_search, calculator | Single character |
| E05 | "Tell me a joke" | "Tell a joke." | `none` | web_search, code_exec, email | Valid English but no tool match — conversational |
| E06 | "weather weather weather weather weather weather weather weather weather weather weather weather weather weather weather weather" | "Get weather information." | `weather` | weather, web_search, calendar | Repetition stress test |
| E07 | "Translate 'hello' to French, then to German, then to Japanese, then back to English and check if it's still 'hello'" | "Translate hello through multiple languages." | `web_search` | web_search, code_exec, calculator | Translation chain — no direct tool, web is closest |
| E08 | "DROP TABLE users; -- What's the weather?" | "Get weather information." | `weather` | weather, web_search, database | SQL injection attempt — distillation should strip it |

---

## 3. Evaluation Criteria

### 3.1 Distillation Quality

For each test prompt, evaluate:

| Criterion | Pass Condition | Weight |
|-----------|---------------|--------|
| **Intent Preservation** | The distilled sentence captures the primary action the user wants to perform | 40% |
| **Conciseness** | Distilled output is ≤30 words | 20% |
| **Specificity** | Key entities (locations, names, file types) are preserved | 20% |
| **Noise Removal** | Filler text, pleasantries, and tangential details are removed | 10% |
| **Safety** | No prompt injection content leaks through distillation | 10% |

### 3.2 Tool Routing Accuracy

| Criterion | Pass Condition | Target |
|-----------|---------------|--------|
| **Top-1 Accuracy** | The #1 routed tool matches the expected primary tool | ≥80% |
| **Top-3 Recall** | All needed tools appear somewhere in the top-3 results | ≥90% |
| **Edge Case Handling** | Edge cases (E01–E08) do not crash and return graceful results | 100% |

### 3.3 Performance

| Metric | Target | Measurement Method |
|--------|--------|--------------------|
| **Distillation Latency** | <500ms per prompt on CPU | `Stopwatch` around `GetResponseAsync` call |
| **Embedding Latency** | <100ms per distilled sentence | `Stopwatch` around embedding generation |
| **Routing Latency** | <50ms per cosine similarity search (8 tools) | `Stopwatch` around `MCPToolRouter.RouteAsync` |
| **Total End-to-End** | <700ms per prompt on CPU | Sum of all three stages |
| **Memory** | <500MB peak working set | `Process.GetCurrentProcess().WorkingSet64` |

---

## 4. Quality Metrics & Scoring

### 4.1 Metrics Definitions

```
Top-1 Accuracy  = (prompts where #1 tool matches expected) / total prompts × 100
Top-3 Recall    = (prompts where all needed tools in top-3) / total prompts × 100
Distillation Accuracy = (prompts where primary intent is preserved) / total prompts × 100
Conciseness Rate = (prompts where distilled output ≤30 words) / total prompts × 100
```

### 4.2 Target Thresholds

| Metric | Minimum Acceptable | Good | Excellent |
|--------|-------------------|------|-----------|
| Top-1 Accuracy | 70% | 80% | 90%+ |
| Top-3 Recall | 80% | 90% | 95%+ |
| Distillation Accuracy | 75% | 85% | 95%+ |
| Conciseness Rate | 90% | 95% | 100% |
| Avg Distillation Latency (CPU) | <1000ms | <500ms | <200ms |

### 4.3 Per-Category Expected Performance

| Category | Expected Top-1 | Expected Top-3 | Rationale |
|----------|---------------|-----------------|-----------|
| Simple (S01–S07) | 95%+ | 100% | Clean intent, should be near-perfect |
| Multi-Intent (M01–M07) | 75%+ | 85%+ | Primary intent extraction is harder |
| Verbose (V01–V07) | 80%+ | 90%+ | Noise removal is the challenge |
| Ambiguous (A01–A07) | 60%+ | 80%+ | Multiple valid interpretations |
| Edge Cases (E01–E08) | N/A | N/A | Focus on graceful handling, not accuracy |

---

## 5. Results Template

> **Instructions:** When the McpToolRouting sample is operational, run each prompt through the pipeline and fill in this table. Each row should be independently evaluated.

### 5.1 Results — Simple Single-Intent

| ID | Actual Distilled Output | Word Count | Intent Preserved? | Top-1 Tool | Top-1 Correct? | Top-3 Tools | Top-3 Recall? | Latency (ms) |
|----|------------------------|------------|-------------------|-----------|---------------|-------------|--------------|---------------|
| S01 | | | | | | | | |
| S02 | | | | | | | | |
| S03 | | | | | | | | |
| S04 | | | | | | | | |
| S05 | | | | | | | | |
| S06 | | | | | | | | |
| S07 | | | | | | | | |

### 5.2 Results — Complex Multi-Intent

| ID | Actual Distilled Output | Word Count | Intent Preserved? | Top-1 Tool | Top-1 Correct? | Top-3 Tools | Top-3 Recall? | Latency (ms) |
|----|------------------------|------------|-------------------|-----------|---------------|-------------|--------------|---------------|
| M01 | | | | | | | | |
| M02 | | | | | | | | |
| M03 | | | | | | | | |
| M04 | | | | | | | | |
| M05 | | | | | | | | |
| M06 | | | | | | | | |
| M07 | | | | | | | | |

### 5.3 Results — Verbose / Chatty

| ID | Actual Distilled Output | Word Count | Intent Preserved? | Top-1 Tool | Top-1 Correct? | Top-3 Tools | Top-3 Recall? | Latency (ms) |
|----|------------------------|------------|-------------------|-----------|---------------|-------------|--------------|---------------|
| V01 | | | | | | | | |
| V02 | | | | | | | | |
| V03 | | | | | | | | |
| V04 | | | | | | | | |
| V05 | | | | | | | | |
| V06 | | | | | | | | |
| V07 | | | | | | | | |

### 5.4 Results — Ambiguous

| ID | Actual Distilled Output | Word Count | Intent Preserved? | Top-1 Tool | Top-1 Correct? | Top-3 Tools | Top-3 Recall? | Latency (ms) |
|----|------------------------|------------|-------------------|-----------|---------------|-------------|--------------|---------------|
| A01 | | | | | | | | |
| A02 | | | | | | | | |
| A03 | | | | | | | | |
| A04 | | | | | | | | |
| A05 | | | | | | | | |
| A06 | | | | | | | | |
| A07 | | | | | | | | |

### 5.5 Results — Edge Cases

| ID | Actual Distilled Output | Crashed? | Graceful? | Top-1 Tool | Notes | Latency (ms) |
|----|------------------------|----------|-----------|-----------|-------|---------------|
| E01 | | | | | | |
| E02 | | | | | | |
| E03 | | | | | | |
| E04 | | | | | | |
| E05 | | | | | | |
| E06 | | | | | | |
| E07 | | | | | | |
| E08 | | | | | | |

### 5.6 Aggregate Metrics

| Metric | Simple | Multi-Intent | Verbose | Ambiguous | Edge | Overall |
|--------|--------|-------------|---------|-----------|------|---------|
| Top-1 Accuracy | | | | | N/A | |
| Top-3 Recall | | | | | N/A | |
| Distillation Accuracy | | | | | N/A | |
| Conciseness Rate | | | | | | |
| Avg Latency (ms) | | | | | | |
| P95 Latency (ms) | | | | | | |

---

## 6. Regression Testing

After initial benchmarking, any change to:
- The distillation system prompt
- The embedding model
- The Qwen2.5-0.5B model version or quantization
- The tool description text

…should trigger a full re-run of this benchmark suite. Track results across runs:

| Run # | Date | Change Description | Top-1 Accuracy | Top-3 Recall | Avg Latency | Pass/Fail |
|-------|------|--------------------|---------------|-------------|-------------|-----------|
| 1 | | Initial baseline | | | | |
| 2 | | | | | | |
| 3 | | | | | | |

**Pass criteria:** No metric drops more than 5 percentage points from the baseline run.

---

## 7. Known Limitations & Considerations

1. **Qwen2.5-0.5B is a small model** — distillation quality may degrade on highly complex or domain-specific prompts. If accuracy targets are not met, consider Qwen2.5-1.5B as a fallback.

2. **Multi-intent prompts are inherently ambiguous** — reasonable people may disagree on the "primary" intent. The expected outputs in section 2.2 represent the team's consensus, not absolute ground truth.

3. **Embedding similarity is sensitive to tool descriptions** — see `tool-description-guide.md` for best practices on writing descriptions that produce good cosine similarity matches.

4. **CPU latency targets assume modern hardware** — targets based on 4-core+ x86_64 CPU with AVX2 support. ARM or older CPUs may need relaxed thresholds.

5. **Edge case "correct" tool is subjective** — for E01–E08, we primarily measure graceful handling (no crashes, no hangs) rather than routing accuracy.
