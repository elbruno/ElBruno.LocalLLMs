using ModelContextProtocol.Protocol;

namespace McpToolRouting;

/// <summary>
/// Provides 40 realistic MCP tool definitions across multiple domains.
/// Rich descriptions improve embedding quality for semantic routing.
/// </summary>
public static class ToolDefinitions
{
    public static Tool[] GetAllTools() =>
    [
        // ── Weather & Environment ──
        new Tool
        {
            Name = "get_current_weather",
            Description = "Get the current weather conditions including temperature, humidity, wind speed, and precipitation for a specific city or geographic coordinates."
        },
        new Tool
        {
            Name = "get_weather_forecast",
            Description = "Get a multi-day weather forecast with high/low temperatures, precipitation probability, and conditions for up to 14 days."
        },
        new Tool
        {
            Name = "get_air_quality",
            Description = "Get the current Air Quality Index (AQI) and pollutant levels for a location, including PM2.5, PM10, ozone, and health recommendations."
        },

        // ── Email & Communication ──
        new Tool
        {
            Name = "send_email",
            Description = "Compose and send an email message to one or more recipients with subject, body text, optional CC/BCC fields, and file attachments."
        },
        new Tool
        {
            Name = "read_inbox",
            Description = "Read recent emails from the inbox. Returns sender, subject, date, and preview text. Supports filtering by read/unread status and date range."
        },
        new Tool
        {
            Name = "search_emails",
            Description = "Search through email messages using keywords, sender address, date range, or attachment presence. Returns matching messages sorted by relevance."
        },
        new Tool
        {
            Name = "send_sms",
            Description = "Send a text message (SMS) to a phone number. Supports message body up to 160 characters for standard SMS or longer for MMS."
        },

        // ── Calendar & Scheduling ──
        new Tool
        {
            Name = "create_calendar_event",
            Description = "Create a new calendar event with title, start/end time, location, description, and optional attendee invitations. Supports recurring events."
        },
        new Tool
        {
            Name = "list_calendar_events",
            Description = "List upcoming calendar events for a date range. Returns event title, time, location, and attendees. Filters by calendar and event type."
        },
        new Tool
        {
            Name = "check_availability",
            Description = "Check free/busy schedule availability for a person or group of people across a specified time range to find open meeting slots."
        },

        // ── File & Document Management ──
        new Tool
        {
            Name = "search_files",
            Description = "Search for files and documents by name, content keywords, file type, or modification date. Searches across local storage and connected cloud drives."
        },
        new Tool
        {
            Name = "read_file",
            Description = "Read the contents of a file given its path. Supports text files, CSV, JSON, and other common formats. Returns file content as text."
        },
        new Tool
        {
            Name = "write_file",
            Description = "Write or overwrite content to a file at a specified path. Creates parent directories if needed. Supports text and binary content."
        },
        new Tool
        {
            Name = "create_document",
            Description = "Create a new document (Word, PDF, or Markdown) with formatted content including headings, lists, tables, and images."
        },

        // ── Mathematics & Calculations ──
        new Tool
        {
            Name = "calculate_expression",
            Description = "Evaluate a mathematical expression. Supports arithmetic, trigonometry, logarithms, exponents, and common math functions."
        },
        new Tool
        {
            Name = "unit_converter",
            Description = "Convert values between units of measurement: length, weight, temperature, volume, speed, area, and data storage units."
        },
        new Tool
        {
            Name = "currency_converter",
            Description = "Convert monetary amounts between currencies using real-time exchange rates. Supports 150+ world currencies and cryptocurrencies."
        },
        new Tool
        {
            Name = "statistics_calculator",
            Description = "Calculate statistical measures on a dataset: mean, median, mode, standard deviation, variance, percentiles, and correlation coefficients."
        },

        // ── Web & Search ──
        new Tool
        {
            Name = "web_search",
            Description = "Search the web for information on any topic. Returns titles, snippets, and URLs of the most relevant web pages and articles."
        },
        new Tool
        {
            Name = "fetch_webpage",
            Description = "Fetch and extract the main content from a webpage URL. Strips navigation and ads, returning clean readable text and metadata."
        },
        new Tool
        {
            Name = "get_news",
            Description = "Get the latest news articles on a topic or from a specific news source. Returns headlines, summaries, publication dates, and source URLs."
        },

        // ── Database & Data ──
        new Tool
        {
            Name = "query_database",
            Description = "Execute a SQL query against a connected database. Supports SELECT, INSERT, UPDATE, and DELETE operations with parameterized queries."
        },
        new Tool
        {
            Name = "list_database_tables",
            Description = "List all tables in a connected database with column names, data types, and row counts. Useful for exploring unfamiliar schemas."
        },
        new Tool
        {
            Name = "export_data_csv",
            Description = "Export query results or dataset to a CSV file. Supports custom delimiters, headers, and encoding options for data interchange."
        },

        // ── Code & Development ──
        new Tool
        {
            Name = "execute_code",
            Description = "Execute a code snippet in a sandboxed environment. Supports Python, JavaScript, C#, and shell scripts. Returns stdout, stderr, and exit code."
        },
        new Tool
        {
            Name = "analyze_code",
            Description = "Analyze source code for bugs, security vulnerabilities, code smells, and best practice violations. Returns issues with severity and fix suggestions."
        },
        new Tool
        {
            Name = "git_operations",
            Description = "Perform Git operations on a repository: clone, pull, push, commit, branch, merge, and view diff. Returns operation status and output."
        },

        // ── Image & Media ──
        new Tool
        {
            Name = "generate_image",
            Description = "Generate an image from a text description using AI. Supports style options like photorealistic, illustration, sketch, and various aspect ratios."
        },
        new Tool
        {
            Name = "resize_image",
            Description = "Resize, crop, or transform an image. Supports dimension changes, format conversion (PNG, JPEG, WebP), and basic filters like blur and sharpen."
        },
        new Tool
        {
            Name = "transcribe_audio",
            Description = "Transcribe speech from an audio file to text. Supports multiple languages, speaker diarization, and timestamp alignment."
        },

        // ── Translation & Language ──
        new Tool
        {
            Name = "translate_text",
            Description = "Translate text between languages. Supports 100+ language pairs with options for formal/informal tone and domain-specific terminology."
        },
        new Tool
        {
            Name = "detect_language",
            Description = "Detect the language of a given text. Returns the detected language code, confidence score, and alternative possible languages."
        },
        new Tool
        {
            Name = "summarize_text",
            Description = "Generate a concise summary of a long text document. Supports extractive and abstractive summarization with adjustable output length."
        },

        // ── Task & Project Management ──
        new Tool
        {
            Name = "create_task",
            Description = "Create a new task or to-do item with title, description, due date, priority level, and optional assignment to a team member."
        },
        new Tool
        {
            Name = "list_tasks",
            Description = "List tasks filtered by status (open, in-progress, done), priority, assignee, project, or due date range. Supports sorting and grouping."
        },
        new Tool
        {
            Name = "update_task_status",
            Description = "Update the status of an existing task: mark as in-progress, completed, or blocked. Add notes and change priority or due date."
        },

        // ── System & Utilities ──
        new Tool
        {
            Name = "get_system_info",
            Description = "Get system information including OS version, CPU usage, memory usage, disk space, and network status for monitoring and diagnostics."
        },
        new Tool
        {
            Name = "set_reminder",
            Description = "Set a reminder for a specific date and time with a custom message. Supports one-time and recurring reminders with notification preferences."
        },
        new Tool
        {
            Name = "generate_password",
            Description = "Generate a secure random password with configurable length, character sets (uppercase, lowercase, digits, symbols), and strength requirements."
        },
        new Tool
        {
            Name = "get_stock_price",
            Description = "Get the current or historical stock price for a ticker symbol. Returns price, change, volume, market cap, and basic financial metrics."
        },
    ];
}
