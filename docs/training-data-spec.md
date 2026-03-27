# Training Data Specification for ElBruno.LocalLLMs Fine-Tuned Models

**Version:** 1.0  
**Date:** 2025-01-27  
**Author:** Mouse (Fine-Tuning Specialist)  
**Target Models:** Qwen2.5-0.5B, Qwen2.5-1.5B, Qwen2.5-3B, Phi-3.5-mini  

## Overview

This document specifies the exact training data format required to fine-tune models for use with the ElBruno.LocalLLMs library. The training data format is **reverse-engineered** from the library's formatter and parser code to ensure fine-tuned models produce output that the library can parse correctly.

**Critical Principle:** Training data must teach models to produce output that matches what `QwenFormatter.cs` and `JsonToolCallParser.cs` expect. If the model doesn't output the exact format the library expects, tool calling will fail.

### Key Requirements

1. **Chat template adherence:** Use ChatML format with `<|im_start|>` and `<|im_end|>` tokens
2. **Tool calling format:** Use `<tool_call>` tags with JSON containing `id`, `name`, and `arguments`
3. **Tool result format:** Use `Tool result for {call_id}: {result}` pattern
4. **RAG grounding:** Teach models to answer from context only and refuse when context is insufficient
5. **Multi-task mixing:** 50% tool calling + 30% RAG + 20% general instruction (prevents catastrophic forgetting)

---

## 1. Tool Calling Training Data

### 1.1 Format Requirements

Based on `QwenFormatter.cs` (lines 108-133) and `JsonToolCallParser.cs` (lines 14-16), the model MUST produce:

```
<tool_call>
{"id": "call_abc123456789", "name": "function_name", "arguments": {"arg1": "value1", "arg2": 42}}
</tool_call>
```

**Critical details:**
- The `<tool_call>` and `</tool_call>` tags are REQUIRED (regex: `<tool_call>\s*(.*?)\s*</tool_call>`)
- JSON must contain: `id` (string), `name` (string), `arguments` (object)
- The `id` field format: `call_{12 hex chars}` (e.g., `call_a1b2c3d4e5f6`)
- Arguments must be a JSON object (not an array, not a primitive)
- Multiple tool calls = multiple `<tool_call>` blocks, NOT a JSON array

### 1.2 System Message Format for Tool Calling

Based on `QwenFormatter.cs` (lines 29-38), the system message includes tool definitions in this exact format:

```
<|im_start|>system
You are a helpful assistant.

You have access to the following tools:

[
  {
    "type": "function",
    "function": {
      "name": "get_weather",
      "description": "Get the current weather for a location",
      "parameters": {
        "type": "object",
        "properties": {
          "location": {
            "type": "string",
            "description": "City name, e.g. San Francisco"
          },
          "unit": {
            "type": "string",
            "enum": ["celsius", "fahrenheit"],
            "description": "Temperature unit"
          }
        },
        "required": ["location"]
      }
    }
  }
]

When you need to call a tool, respond with a JSON object in this format:
{"name": "tool_name", "arguments": {"arg1": "value1"}}
<|im_end|>
```

**Note:** The instruction says `{"name": "tool_name", "arguments": {...}}`, but the model MUST output `{"id": "call_...", "name": "...", "arguments": {...}}` — the `id` field is added by the model.

### 1.3 Tool Calling Examples

#### Example 1: Single Tool Call

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"get_weather\",\n      \"description\": \"Get the current weather for a location\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"location\": {\n            \"type\": \"string\",\n            \"description\": \"City name\"\n          },\n          \"unit\": {\n            \"type\": \"string\",\n            \"enum\": [\"celsius\", \"fahrenheit\"]\n          }\n        },\n        \"required\": [\"location\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "What's the weather like in Seattle?"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_9a8b7c6d5e4f\", \"name\": \"get_weather\", \"arguments\": {\"location\": \"Seattle\", \"unit\": \"fahrenheit\"}}\n</tool_call>"
    }
  ]
}
```

#### Example 2: Multiple Tool Calls in One Turn

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"get_weather\",\n      \"description\": \"Get current weather\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"location\": {\"type\": \"string\"}\n        },\n        \"required\": [\"location\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "Compare the weather in New York and Los Angeles"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_1a2b3c4d5e6f\", \"name\": \"get_weather\", \"arguments\": {\"location\": \"New York\"}}\n</tool_call>\n<tool_call>\n{\"id\": \"call_7g8h9i0j1k2l\", \"name\": \"get_weather\", \"arguments\": {\"location\": \"Los Angeles\"}}\n</tool_call>"
    }
  ]
}
```

#### Example 3: Tool Call with Complex Arguments (Nested Objects, Arrays)

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"create_calendar_event\",\n      \"description\": \"Create a new calendar event\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"title\": {\"type\": \"string\"},\n          \"start\": {\"type\": \"string\"},\n          \"end\": {\"type\": \"string\"},\n          \"attendees\": {\n            \"type\": \"array\",\n            \"items\": {\"type\": \"string\"}\n          },\n          \"location\": {\n            \"type\": \"object\",\n            \"properties\": {\n              \"address\": {\"type\": \"string\"},\n              \"room\": {\"type\": \"string\"}\n            }\n          }\n        },\n        \"required\": [\"title\", \"start\", \"end\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "Schedule a team meeting tomorrow at 2 PM in Conference Room A with john@example.com and sarah@example.com"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_3m4n5o6p7q8r\", \"name\": \"create_calendar_event\", \"arguments\": {\"title\": \"Team Meeting\", \"start\": \"2025-01-28T14:00:00\", \"end\": \"2025-01-28T15:00:00\", \"attendees\": [\"john@example.com\", \"sarah@example.com\"], \"location\": {\"room\": \"Conference Room A\"}}}\n</tool_call>"
    }
  ]
}
```

#### Example 4: Multi-Turn Tool Call Chain (Result Feeds Next Call)

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"search_products\",\n      \"description\": \"Search for products\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"query\": {\"type\": \"string\"}\n        },\n        \"required\": [\"query\"]\n      }\n    }\n  },\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"get_product_details\",\n      \"description\": \"Get detailed information about a product\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"product_id\": {\"type\": \"string\"}\n        },\n        \"required\": [\"product_id\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "Find me a good laptop and tell me about the top result"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_9s8t7u6v5w4x\", \"name\": \"search_products\", \"arguments\": {\"query\": \"laptop\"}}\n</tool_call>"
    },
    {
      "from": "user",
      "value": "Tool result for call_9s8t7u6v5w4x: [{\"id\": \"prod_123\", \"name\": \"Dell XPS 15\", \"price\": 1299}, {\"id\": \"prod_456\", \"name\": \"MacBook Pro\", \"price\": 1999}]"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_3y2z1a0b9c8d\", \"name\": \"get_product_details\", \"arguments\": {\"product_id\": \"prod_123\"}}\n</tool_call>"
    },
    {
      "from": "user",
      "value": "Tool result for call_3y2z1a0b9c8d: {\"id\": \"prod_123\", \"name\": \"Dell XPS 15\", \"price\": 1299, \"specs\": {\"cpu\": \"Intel i7\", \"ram\": \"16GB\", \"storage\": \"512GB SSD\"}, \"rating\": 4.5}"
    },
    {
      "from": "assistant",
      "value": "I found the Dell XPS 15 as the top result. It's priced at $1,299 and features an Intel i7 processor, 16GB RAM, and a 512GB SSD. It has a 4.5-star rating, making it a solid choice for a laptop."
    }
  ]
}
```

#### Example 5: Tool Call with Error Handling

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"send_email\",\n      \"description\": \"Send an email\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"to\": {\"type\": \"string\"},\n          \"subject\": {\"type\": \"string\"},\n          \"body\": {\"type\": \"string\"}\n        },\n        \"required\": [\"to\", \"subject\", \"body\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "Send an email to john@example.com saying the meeting is cancelled"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_7e6f5g4h3i2j\", \"name\": \"send_email\", \"arguments\": {\"to\": \"john@example.com\", \"subject\": \"Meeting Cancelled\", \"body\": \"The meeting scheduled for today has been cancelled. We will reschedule soon.\"}}\n</tool_call>"
    },
    {
      "from": "user",
      "value": "Tool result for call_7e6f5g4h3i2j: Error: Invalid email address format"
    },
    {
      "from": "assistant",
      "value": "I apologize, but there was an error sending the email. The system reported that the email address format is invalid. Could you please verify the correct email address for John?"
    }
  ]
}
```

#### Example 6: No Tool Call Needed (Negative Example)

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"get_weather\",\n      \"description\": \"Get current weather\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"location\": {\"type\": \"string\"}\n        },\n        \"required\": [\"location\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "What's the capital of France?"
    },
    {
      "from": "assistant",
      "value": "The capital of France is Paris."
    }
  ]
}
```

#### Example 7: Parallel Tool Calls (Different Functions)

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"get_stock_price\",\n      \"description\": \"Get current stock price\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"symbol\": {\"type\": \"string\"}\n        },\n        \"required\": [\"symbol\"]\n      }\n    }\n  },\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"get_company_info\",\n      \"description\": \"Get company information\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"symbol\": {\"type\": \"string\"}\n        },\n        \"required\": [\"symbol\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "Tell me about Apple Inc and its current stock price"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_1k2l3m4n5o6p\", \"name\": \"get_stock_price\", \"arguments\": {\"symbol\": \"AAPL\"}}\n</tool_call>\n<tool_call>\n{\"id\": \"call_7q8r9s0t1u2v\", \"name\": \"get_company_info\", \"arguments\": {\"symbol\": \"AAPL\"}}\n</tool_call>"
    }
  ]
}
```

#### Example 8: Tool Call with Empty Arguments

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"get_current_time\",\n      \"description\": \"Get the current time\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {}\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "What time is it?"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_3w4x5y6z7a8b\", \"name\": \"get_current_time\", \"arguments\": {}}\n</tool_call>"
    }
  ]
}
```

#### Example 9: Tool Call with Reasoning First

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"calculate\",\n      \"description\": \"Perform mathematical calculations\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"expression\": {\"type\": \"string\"}\n        },\n        \"required\": [\"expression\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "If I buy 3 items at $15.99 each and there's a 20% discount, what's the total?"
    },
    {
      "from": "assistant",
      "value": "I'll calculate this step by step. First, I need to find the subtotal (3 × $15.99), then apply the 20% discount.\n<tool_call>\n{\"id\": \"call_9c0d1e2f3g4h\", \"name\": \"calculate\", \"arguments\": {\"expression\": \"3 * 15.99 * 0.8\"}}\n</tool_call>"
    }
  ]
}
```

#### Example 10: Tool Call with Optional Parameters

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"search_articles\",\n      \"description\": \"Search for articles\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"query\": {\"type\": \"string\"},\n          \"max_results\": {\"type\": \"integer\", \"description\": \"Maximum number of results (default: 10)\"},\n          \"sort_by\": {\"type\": \"string\", \"enum\": [\"relevance\", \"date\"], \"description\": \"Sort order (default: relevance)\"}\n        },\n        \"required\": [\"query\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "Search for recent articles about AI and show me the top 5"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_5i6j7k8l9m0n\", \"name\": \"search_articles\", \"arguments\": {\"query\": \"AI\", \"max_results\": 5, \"sort_by\": \"date\"}}\n</tool_call>"
    }
  ]
}
```

#### Example 11: Tool Call Followed by Synthesis

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"get_exchange_rate\",\n      \"description\": \"Get currency exchange rate\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"from_currency\": {\"type\": \"string\"},\n          \"to_currency\": {\"type\": \"string\"}\n        },\n        \"required\": [\"from_currency\", \"to_currency\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "How much is 100 USD in EUR?"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_1o2p3q4r5s6t\", \"name\": \"get_exchange_rate\", \"arguments\": {\"from_currency\": \"USD\", \"to_currency\": \"EUR\"}}\n</tool_call>"
    },
    {
      "from": "user",
      "value": "Tool result for call_1o2p3q4r5s6t: {\"rate\": 0.92, \"timestamp\": \"2025-01-27T10:00:00Z\"}"
    },
    {
      "from": "assistant",
      "value": "Based on the current exchange rate of 0.92, 100 USD is approximately 92 EUR. Please note that exchange rates fluctuate constantly, so this is the rate as of January 27, 2025 at 10:00 AM UTC."
    }
  ]
}
```

#### Example 12: Multiple Sequential Tool Calls (Dependency Chain)

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"get_user_id\",\n      \"description\": \"Get user ID from username\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"username\": {\"type\": \"string\"}\n        },\n        \"required\": [\"username\"]\n      }\n    }\n  },\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"get_user_posts\",\n      \"description\": \"Get posts for a user\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"user_id\": {\"type\": \"string\"}\n        },\n        \"required\": [\"user_id\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "Show me posts from user @johndoe"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_7u8v9w0x1y2z\", \"name\": \"get_user_id\", \"arguments\": {\"username\": \"johndoe\"}}\n</tool_call>"
    },
    {
      "from": "user",
      "value": "Tool result for call_7u8v9w0x1y2z: {\"user_id\": \"user_12345\"}"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_3a4b5c6d7e8f\", \"name\": \"get_user_posts\", \"arguments\": {\"user_id\": \"user_12345\"}}\n</tool_call>"
    },
    {
      "from": "user",
      "value": "Tool result for call_3a4b5c6d7e8f: [{\"id\": \"post_1\", \"title\": \"Hello World\", \"date\": \"2025-01-20\"}, {\"id\": \"post_2\", \"title\": \"My Second Post\", \"date\": \"2025-01-25\"}]"
    },
    {
      "from": "assistant",
      "value": "I found 2 posts from @johndoe:\n1. \"Hello World\" (January 20, 2025)\n2. \"My Second Post\" (January 25, 2025)"
    }
  ]
}
```

---

## 2. RAG Grounded Answering Training Data

### 2.1 Format Requirements

RAG training data teaches the model to:
1. Answer ONLY from the provided context
2. Explicitly refuse when the answer is not in the context
3. Cite sources when appropriate
4. Distinguish between grounded facts and speculation

### 2.2 Context Injection Format

The context is injected in the system message or as a separate context block in the user message:

```
<|im_start|>system
You are a helpful assistant that answers questions based only on the provided context.
If the answer is not in the context, say "I don't have enough information to answer that."
Never make up information or answer from general knowledge.
<|im_end|>
<|im_start|>user
Context:
---
The Eiffel Tower is a wrought-iron lattice tower on the Champ de Mars in Paris, France. 
It is named after the engineer Gustave Eiffel, whose company designed and built the tower. 
Constructed from 1887 to 1889, it was initially criticized by some of France's leading artists 
and intellectuals for its design, but it has become a global cultural icon of France and one 
of the most recognizable structures in the world. The tower is 330 metres (1,083 ft) tall.
---

Question: How tall is the Eiffel Tower?
<|im_end|>
```

### 2.3 RAG Examples

#### Example 1: Simple Grounded Answer

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that answers questions based only on the provided context. If the answer is not in the context, say \"I don't have enough information to answer that.\" Never make up information."
    },
    {
      "from": "user",
      "value": "Context:\n---\nPython is a high-level, interpreted programming language. It was created by Guido van Rossum and first released in 1991. Python's design philosophy emphasizes code readability with its notable use of significant whitespace.\n---\n\nQuestion: Who created Python?"
    },
    {
      "from": "assistant",
      "value": "Python was created by Guido van Rossum."
    }
  ]
}
```

#### Example 2: Information Not in Context (Refusal)

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that answers questions based only on the provided context. If the answer is not in the context, say \"I don't have enough information to answer that.\" Never make up information."
    },
    {
      "from": "user",
      "value": "Context:\n---\nThe Sun is the star at the center of the Solar System. It is a nearly perfect sphere of hot plasma, with internal convective motion that generates a magnetic field via a dynamo process.\n---\n\nQuestion: How many planets are in the Solar System?"
    },
    {
      "from": "assistant",
      "value": "I don't have enough information to answer that. The provided context describes the Sun but doesn't mention the number of planets in the Solar System."
    }
  ]
}
```

#### Example 3: Multiple Context Sources with Citation

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that answers questions based only on the provided context. Cite the source document when answering. If the answer is not in the context, say \"I don't have enough information to answer that.\""
    },
    {
      "from": "user",
      "value": "Context:\n---\n[Document 1: company_policies.txt]\nAll employees are entitled to 15 days of paid vacation per year. Vacation days must be requested at least 2 weeks in advance through the HR portal.\n\n[Document 2: benefits_guide.pdf]\nEmployees receive health insurance coverage starting on their first day of employment. The company covers 80% of the premium cost.\n---\n\nQuestion: What is the vacation policy?"
    },
    {
      "from": "assistant",
      "value": "According to company_policies.txt, all employees are entitled to 15 days of paid vacation per year. Vacation days must be requested at least 2 weeks in advance through the HR portal."
    }
  ]
}
```

#### Example 4: Synthesizing from Multiple Passages

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that answers questions based only on the provided context. If the answer is not in the context, say \"I don't have enough information to answer that.\""
    },
    {
      "from": "user",
      "value": "Context:\n---\nMicrosoft was founded by Bill Gates and Paul Allen on April 4, 1975. The company initially focused on developing and selling BASIC interpreters for the Altair 8800.\n\nIn 1980, Microsoft partnered with IBM to provide an operating system for IBM's first personal computer. This operating system, MS-DOS, became the foundation for Microsoft's dominance in the PC operating system market.\n\nWindows 1.0 was released in 1985 as a graphical operating system shell for MS-DOS.\n---\n\nQuestion: What major partnerships and products helped establish Microsoft's early success?"
    },
    {
      "from": "assistant",
      "value": "Microsoft's early success was built on several key developments:\n\n1. The company started by developing BASIC interpreters for the Altair 8800 in 1975.\n2. A crucial partnership with IBM in 1980 led to MS-DOS, which became the foundation for Microsoft's dominance in the PC operating system market.\n3. Windows 1.0 was released in 1985 as a graphical shell for MS-DOS, further expanding their product offerings.\n\nThe IBM partnership and MS-DOS were particularly significant in establishing Microsoft's market position."
    }
  ]
}
```

#### Example 5: Partial Information in Context

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that answers questions based only on the provided context. If you can only partially answer, provide what you know and indicate what's missing."
    },
    {
      "from": "user",
      "value": "Context:\n---\nThe Apollo 11 mission landed the first humans on the Moon. The mission was launched on July 16, 1969. Neil Armstrong and Buzz Aldrin walked on the lunar surface while Michael Collins remained in orbit.\n---\n\nQuestion: When did Apollo 11 launch and return to Earth?"
    },
    {
      "from": "assistant",
      "value": "According to the context, Apollo 11 launched on July 16, 1969. However, the provided context does not mention when the mission returned to Earth."
    }
  ]
}
```

#### Example 6: Conflicting Information Handling

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that answers questions based only on the provided context. If there's conflicting information, acknowledge both sources."
    },
    {
      "from": "user",
      "value": "Context:\n---\n[Source A - 2020 report]: The company's revenue was $500 million in 2019.\n\n[Source B - 2021 report]: The company's 2019 revenue was $485 million after accounting adjustments.\n---\n\nQuestion: What was the company's 2019 revenue?"
    },
    {
      "from": "assistant",
      "value": "There are two different figures in the provided context for the company's 2019 revenue:\n- Source A (2020 report) states the revenue was $500 million\n- Source B (2021 report) states it was $485 million after accounting adjustments\n\nThe difference appears to be due to accounting adjustments made after the initial reporting."
    }
  ]
}
```

#### Example 7: List/Enumeration from Context

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that answers questions based only on the provided context."
    },
    {
      "from": "user",
      "value": "Context:\n---\nOur software supports the following programming languages: C#, Python, JavaScript, Go, and Rust. TypeScript support is currently in beta testing.\n---\n\nQuestion: What programming languages are supported?"
    },
    {
      "from": "assistant",
      "value": "The software supports the following programming languages:\n- C#\n- Python\n- JavaScript\n- Go\n- Rust\n\nAdditionally, TypeScript support is currently in beta testing."
    }
  ]
}
```

#### Example 8: Numerical/Factual Extraction

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that answers questions based only on the provided context."
    },
    {
      "from": "user",
      "value": "Context:\n---\nThe quarterly sales report shows the following figures:\n- Q1 2024: $1.2 million\n- Q2 2024: $1.5 million\n- Q3 2024: $1.8 million\n- Q4 2024: $2.1 million\nTotal annual revenue: $6.6 million, representing a 15% increase from 2023.\n---\n\nQuestion: What was the total revenue for 2024 and how does it compare to 2023?"
    },
    {
      "from": "assistant",
      "value": "The total revenue for 2024 was $6.6 million, which represents a 15% increase from 2023."
    }
  ]
}
```

#### Example 9: Definition/Explanation from Context

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that answers questions based only on the provided context."
    },
    {
      "from": "user",
      "value": "Context:\n---\nQuantum entanglement is a physical phenomenon that occurs when a group of particles are generated, interact, or share spatial proximity in a way such that the quantum state of each particle of the group cannot be described independently of the state of the others, including when the particles are separated by a large distance.\n---\n\nQuestion: What is quantum entanglement?"
    },
    {
      "from": "assistant",
      "value": "Quantum entanglement is a physical phenomenon that occurs when particles are generated, interact, or share spatial proximity in such a way that the quantum state of each particle cannot be described independently of the others. This connection persists even when the particles are separated by a large distance."
    }
  ]
}
```

#### Example 10: Empty/Insufficient Context

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that answers questions based only on the provided context. If the context is empty or insufficient, say so clearly."
    },
    {
      "from": "user",
      "value": "Context:\n---\n(no relevant information found)\n---\n\nQuestion: What is the company's return policy?"
    },
    {
      "from": "assistant",
      "value": "I don't have enough information to answer that question. The provided context does not contain any information about the company's return policy."
    }
  ]
}
```

---

## 3. Chat Template Adherence Training Data

### 3.1 Format Requirements

Every training example MUST follow the exact ChatML format to prevent the model from hallucinating extra turns or breaking the template:

```
<|im_start|>role
content
<|im_end|>
```

### 3.2 Template Adherence Examples

#### Example 1: Clean Single-Turn Exchange

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant."
    },
    {
      "from": "user",
      "value": "What is 2 + 2?"
    },
    {
      "from": "assistant",
      "value": "2 + 2 equals 4."
    }
  ]
}
```

#### Example 2: Multi-Turn Conversation

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant."
    },
    {
      "from": "user",
      "value": "Tell me about the solar system."
    },
    {
      "from": "assistant",
      "value": "The solar system consists of the Sun and all the objects that orbit around it, including eight planets, their moons, asteroids, comets, and other celestial bodies."
    },
    {
      "from": "user",
      "value": "Which planet is the largest?"
    },
    {
      "from": "assistant",
      "value": "Jupiter is the largest planet in our solar system. It's so large that more than 1,300 Earths could fit inside it."
    }
  ]
}
```

#### Example 3: Empty System Message (Should Still Be Present)

```json
{
  "conversations": [
    {
      "from": "system",
      "value": ""
    },
    {
      "from": "user",
      "value": "Hello!"
    },
    {
      "from": "assistant",
      "value": "Hello! How can I help you today?"
    }
  ]
}
```

#### Example 4: Very Long Context

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that summarizes documents."
    },
    {
      "from": "user",
      "value": "Please summarize this document:\n\n[Insert 2000+ word document here]\n\nWhat are the key points?"
    },
    {
      "from": "assistant",
      "value": "Here are the key points from the document:\n\n1. [Point 1]\n2. [Point 2]\n3. [Point 3]\n\n[Summary continues...]"
    }
  ]
}
```

#### Example 5: Multiple System Messages (Edge Case)

**Note:** The library expects ONE system message at the start. Training data should reflect this:

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant. You are an expert in programming."
    },
    {
      "from": "user",
      "value": "Write a Python function to calculate factorial."
    },
    {
      "from": "assistant",
      "value": "Here's a Python function to calculate factorial:\n\n```python\ndef factorial(n):\n    if n == 0 or n == 1:\n        return 1\n    else:\n        return n * factorial(n - 1)\n```"
    }
  ]
}
```

#### Example 6: No System Message (Should Add Default)

**Training data format:** If no system message exists, the library adds a default "You are a helpful assistant." Include examples with and without explicit system messages:

```json
{
  "conversations": [
    {
      "from": "user",
      "value": "Hi there!"
    },
    {
      "from": "assistant",
      "value": "Hello! How can I assist you today?"
    }
  ]
}
```

**During training conversion, this should be preprocessed to:**

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant."
    },
    {
      "from": "user",
      "value": "Hi there!"
    },
    {
      "from": "assistant",
      "value": "Hello! How can I assist you today?"
    }
  ]
}
```

#### Example 7: Assistant Stops Cleanly (No Mid-Sentence Cutoff)

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a concise assistant."
    },
    {
      "from": "user",
      "value": "What's the weather like?"
    },
    {
      "from": "assistant",
      "value": "I don't have access to real-time weather data. Please check a weather service for current conditions."
    }
  ]
}
```

#### Example 8: Multi-Paragraph Assistant Response

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant."
    },
    {
      "from": "user",
      "value": "Explain photosynthesis."
    },
    {
      "from": "assistant",
      "value": "Photosynthesis is the process by which plants, algae, and some bacteria convert light energy into chemical energy stored in glucose.\n\nThe process occurs in two main stages:\n1. Light-dependent reactions: These take place in the thylakoid membranes and convert light energy into ATP and NADPH.\n2. Light-independent reactions (Calvin cycle): These occur in the stroma and use ATP and NADPH to convert CO2 into glucose.\n\nThe overall equation is: 6CO2 + 6H2O + light energy → C6H12O6 + 6O2"
    }
  ]
}
```

#### Example 9: Code Block Formatting

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a programming assistant."
    },
    {
      "from": "user",
      "value": "Write a C# method to check if a string is a palindrome."
    },
    {
      "from": "assistant",
      "value": "```csharp\npublic static bool IsPalindrome(string input)\n{\n    if (string.IsNullOrEmpty(input))\n        return false;\n    \n    string cleaned = input.ToLower().Replace(\" \", \"\");\n    int left = 0;\n    int right = cleaned.Length - 1;\n    \n    while (left < right)\n    {\n        if (cleaned[left] != cleaned[right])\n            return false;\n        left++;\n        right--;\n    }\n    \n    return true;\n}\n```\n\nThis method converts the input to lowercase, removes spaces, and checks if the string reads the same forwards and backwards."
    }
  ]
}
```

#### Example 10: Special Characters and Escaping

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant."
    },
    {
      "from": "user",
      "value": "What does the regex pattern ^[a-z]+$ match?"
    },
    {
      "from": "assistant",
      "value": "The regex pattern ^[a-z]+$ matches:\n- ^ = Start of string\n- [a-z]+ = One or more lowercase letters (a-z)\n- $ = End of string\n\nSo it matches strings that contain only lowercase letters from start to end, with at least one character. Examples: \"hello\", \"world\", \"abc\"."
    }
  ]
}
```

---

## 4. Dataset Format

### 4.1 Recommended Format: ShareGPT JSON

The ShareGPT format is the industry standard for conversational fine-tuning data:

```json
[
  {
    "conversations": [
      {
        "from": "system",
        "value": "System message content"
      },
      {
        "from": "user",
        "value": "User message content"
      },
      {
        "from": "assistant",
        "value": "Assistant response content"
      }
    ]
  },
  {
    "conversations": [
      ...
    ]
  }
]
```

**Key fields:**
- `conversations`: Array of messages forming a single conversation
- `from`: Role — must be `"system"`, `"user"`, or `"assistant"`
- `value`: Message content (text, tool calls, tool results)

### 4.2 Alternative Format: Alpaca/Instruct

For single-turn instruction-following data:

```json
[
  {
    "instruction": "User instruction",
    "input": "Optional context or additional input",
    "output": "Expected assistant response"
  }
]
```

**Note:** ShareGPT is preferred because it supports multi-turn conversations and tool calling.

### 4.3 Complete Training Dataset Structure

A production-ready dataset should combine all task types:

```
training_data/
├── tool_calling/
│   ├── single_call.json           (500-1000 examples)
│   ├── multi_call.json            (300-500 examples)
│   ├── complex_args.json          (200-300 examples)
│   ├── negative_examples.json     (100-200 examples)
│   └── error_handling.json        (100-200 examples)
├── rag/
│   ├── grounded_qa.json           (1000-1500 examples)
│   ├── refusal.json               (200-300 examples)
│   └── multi_source.json          (200-300 examples)
├── general/
│   ├── conversation.json          (500-1000 examples)
│   ├── instruction_following.json (500-1000 examples)
│   └── coding.json                (200-500 examples)
└── combined_train.json            (All examples merged)
```

### 4.4 Dataset Mixing Ratios

For a 5,000-example training dataset:

| Category | Examples | Percentage | Purpose |
|----------|----------|------------|---------|
| Tool Calling | 2,500 | 50% | Primary task — highest value |
| RAG Grounded QA | 1,500 | 30% | Critical for document-based apps |
| General Instruction | 1,000 | 20% | Prevents catastrophic forgetting |
| **Total** | **5,000** | **100%** | Production-ready dataset |

**For PoC (1,000 examples):**
- Tool Calling: 500 (50%)
- RAG: 300 (30%)
- General: 200 (20%)

### 4.5 Train/Validation/Test Split

```
Total: 5,000 examples
├── Train: 4,000 (80%)
├── Validation: 500 (10%)
└── Test (hold-out): 500 (10%)
```

**Important:** The test set should NEVER be used during training. It's for final evaluation only.

### 4.6 Data Quality Validation Schema

Every training example should pass these checks:

```python
def validate_training_example(example):
    """Validates a ShareGPT training example."""
    assert "conversations" in example
    assert len(example["conversations"]) >= 2  # At least user + assistant
    
    for msg in example["conversations"]:
        assert "from" in msg
        assert msg["from"] in ["system", "user", "assistant"]
        assert "value" in msg
        assert len(msg["value"]) > 0  # No empty messages
    
    # First message should be system (if present) or user
    assert example["conversations"][0]["from"] in ["system", "user"]
    
    # Tool calls should have proper format
    for msg in example["conversations"]:
        if "<tool_call>" in msg["value"]:
            assert msg["from"] == "assistant"
            assert "</tool_call>" in msg["value"]
            # Validate JSON inside tool_call tags
            validate_tool_call_json(msg["value"])
    
    return True
```

---

## 5. Existing Datasets to Leverage

### 5.1 Tool Calling Datasets

#### Glaive Function Calling v2 (Recommended)
- **HuggingFace:** `glaiveai/glaive-function-calling-v2`
- **Size:** 113,000 examples
- **Format:** ShareGPT-compatible
- **License:** Apache 2.0 ✅
- **Quality:** High — industry gold standard
- **Usage:** Filter to 1,000-5,000 most relevant examples (weather, database, API calls)

```python
from datasets import load_dataset

dataset = load_dataset("glaiveai/glaive-function-calling-v2")
# Filter by relevance, deduplicate, and convert to library's tool format
```

#### NousResearch Hermes Function Calling
- **HuggingFace:** `NousResearch/hermes-function-calling-v1`
- **Size:** 50,000+ examples
- **License:** Apache 2.0 ✅
- **Quality:** High — multi-step tool calling
- **Usage:** Supplement Glaive with complex multi-turn sequences

#### Salesforce xLAM Function Calling
- **HuggingFace:** `Salesforce/xlam-function-calling-60k`
- **Size:** 60,000 examples
- **License:** Apache 2.0 ✅
- **Quality:** Very high — includes parallel tool calls
- **Usage:** Advanced tool calling patterns

### 5.2 RAG/Grounded QA Datasets

#### SQuAD 2.0 (Stanford Question Answering Dataset)
- **HuggingFace:** `squad_v2`
- **Size:** 150,000+ QA pairs
- **License:** Apache 2.0 ✅
- **Quality:** Excellent — includes unanswerable questions
- **Usage:** Convert to `context + question → answer or refusal` format

#### MS MARCO (Microsoft Machine Reading Comprehension)
- **HuggingFace:** `ms_marco`
- **Size:** 1,000,000+ examples
- **License:** Microsoft Research License (✅ usable for training)
- **Quality:** High — real search queries
- **Usage:** Realistic grounded QA with web search context

#### Natural Questions (Google)
- **HuggingFace:** `natural_questions`
- **Size:** 307,000+ examples
- **License:** Apache 2.0 ✅
- **Quality:** Very high — Wikipedia-based
- **Usage:** Long-form grounded answers with citations

#### HotpotQA (Multi-Hop Reasoning)
- **HuggingFace:** `hotpot_qa`
- **Size:** 113,000 examples
- **License:** CC BY-SA 4.0 ✅
- **Quality:** Excellent — requires synthesizing multiple passages
- **Usage:** Complex RAG scenarios with multi-document retrieval

### 5.3 General Instruction Following Datasets

#### Dolly-15K (Databricks)
- **HuggingFace:** `databricks/databricks-dolly-15k`
- **Size:** 15,000 examples
- **License:** CC BY-SA 3.0 ✅
- **Quality:** Very high — human-generated
- **Usage:** General instruction tuning (mix 10-20%)

#### OpenOrca
- **HuggingFace:** `Open-Orca/OpenOrca`
- **Size:** 4,000,000+ examples (use subset)
- **License:** MIT ✅
- **Quality:** High — GPT-4 distilled
- **Usage:** General chat and reasoning (select 500-1,000 best examples)

#### Alpaca Cleaned
- **HuggingFace:** `yahma/alpaca-cleaned`
- **Size:** 52,000 examples
- **License:** Apache 2.0 ✅
- **Quality:** Good — cleaned version of original Alpaca
- **Usage:** Single-turn instruction following

### 5.4 Dataset Conversion Pipeline

**Step 1: Load existing dataset**
```python
from datasets import load_dataset
squad = load_dataset("squad_v2", split="train")
```

**Step 2: Convert to ShareGPT format**
```python
def convert_squad_to_sharegpt(example):
    """Converts SQuAD example to ShareGPT format."""
    context = example["context"]
    question = example["question"]
    
    if example["answers"]["text"]:  # Answerable
        answer = example["answers"]["text"][0]
        assistant_msg = answer
    else:  # Unanswerable
        assistant_msg = "I don't have enough information to answer that. The provided context does not contain the answer to this question."
    
    return {
        "conversations": [
            {
                "from": "system",
                "value": "You are a helpful assistant that answers questions based only on the provided context. If the answer is not in the context, say \"I don't have enough information to answer that.\""
            },
            {
                "from": "user",
                "value": f"Context:\n---\n{context}\n---\n\nQuestion: {question}"
            },
            {
                "from": "assistant",
                "value": assistant_msg
            }
        ]
    }

sharegpt_data = [convert_squad_to_sharegpt(ex) for ex in squad]
```

**Step 3: Validate and filter**
```python
validated = [ex for ex in sharegpt_data if validate_training_example(ex)]
```

**Step 4: Save to JSON**
```python
import json
with open("squad_sharegpt.json", "w") as f:
    json.dump(validated, f, indent=2)
```

### 5.5 License Compatibility Matrix

| Dataset | License | Commercial Use | Attribution Required | Share-Alike |
|---------|---------|----------------|----------------------|-------------|
| Glaive Function Calling v2 | Apache 2.0 | ✅ Yes | Optional | No |
| Hermes Function Calling | Apache 2.0 | ✅ Yes | Optional | No |
| xLAM Function Calling | Apache 2.0 | ✅ Yes | Optional | No |
| SQuAD 2.0 | Apache 2.0 | ✅ Yes | Optional | No |
| MS MARCO | MS Research | ✅ Yes | Yes | No |
| Natural Questions | Apache 2.0 | ✅ Yes | Optional | No |
| HotpotQA | CC BY-SA 4.0 | ✅ Yes | Yes | Yes (same license) |
| Dolly-15K | CC BY-SA 3.0 | ✅ Yes | Yes | Yes (same license) |
| OpenOrca | MIT | ✅ Yes | Optional | No |
| Alpaca Cleaned | Apache 2.0 | ✅ Yes | Optional | No |

**Safe to use:** All listed datasets are compatible with commercial training and model distribution.

---

## 6. Quality Criteria

### 6.1 Pre-Training Validation Checklist

Before fine-tuning, ALL training data must pass:

#### Structural Validation
- [ ] Every example is valid JSON
- [ ] Every conversation has at least 2 messages (user + assistant)
- [ ] All messages have `from` and `value` fields
- [ ] Role values are exactly `"system"`, `"user"`, or `"assistant"`
- [ ] No empty `value` fields
- [ ] System message (if present) is first

#### Tool Calling Validation
- [ ] All `<tool_call>` tags have matching `</tool_call>` tags
- [ ] JSON inside `<tool_call>` tags is valid
- [ ] Every tool call has `id`, `name`, and `arguments` fields
- [ ] Tool call IDs follow pattern: `call_[12 hex chars]`
- [ ] Arguments are JSON objects (not arrays or primitives)
- [ ] Tool result messages reference valid call IDs

#### RAG Validation
- [ ] Context is clearly delimited (e.g., `---` or `Context:` prefix)
- [ ] Answers are grounded in provided context
- [ ] Refusal examples teach proper "I don't know" responses
- [ ] No hallucinated information in assistant responses

#### Content Quality
- [ ] No toxic, offensive, or harmful content
- [ ] Accurate factual information (for general knowledge)
- [ ] Consistent formatting across examples
- [ ] Diverse topics and scenarios (not repetitive)

### 6.2 Common Training Data Mistakes That Hurt Model Quality

#### 1. **Inconsistent Tool Call Format**
❌ **Bad:**
```json
"value": "<function_call>{\"name\": \"get_weather\"}</function_call>"
```
✅ **Good:**
```json
"value": "<tool_call>\n{\"id\": \"call_abc123456789\", \"name\": \"get_weather\", \"arguments\": {\"location\": \"Seattle\"}}\n</tool_call>"
```

#### 2. **Missing Tool Results in Multi-Turn Sequences**
❌ **Bad:**
```json
{
  "from": "assistant",
  "value": "<tool_call>...</tool_call>"
},
{
  "from": "assistant",  // WRONG: Assistant talking to itself
  "value": "Based on the weather data..."
}
```
✅ **Good:**
```json
{
  "from": "assistant",
  "value": "<tool_call>...</tool_call>"
},
{
  "from": "user",
  "value": "Tool result for call_abc: {temperature: 72}"
},
{
  "from": "assistant",
  "value": "Based on the weather data..."
}
```

#### 3. **RAG Data That Hallucinates Beyond Context**
❌ **Bad:**
```json
{
  "from": "user",
  "value": "Context: Python was created in 1991.\n\nQuestion: What language influenced Python?"
},
{
  "from": "assistant",
  "value": "Python was influenced by ABC and Modula-3."  // NOT IN CONTEXT
}
```
✅ **Good:**
```json
{
  "from": "assistant",
  "value": "I don't have enough information to answer that. The context states when Python was created but doesn't mention which languages influenced it."
}
```

#### 4. **Template Token Errors**
❌ **Bad:**
```json
{
  "from": "assistant",
  "value": "The answer is 42.<|im_end|>\n<|im_start|>user\nWhat about..."
}
```
The training framework will add `<|im_start|>` and `<|im_end|>` — don't include them in the message content.

#### 5. **Empty or Whitespace-Only Messages**
❌ **Bad:**
```json
{"from": "system", "value": ""}
```
✅ **Good:**
```json
{"from": "system", "value": "You are a helpful assistant."}
```

#### 6. **Tool Calls in User Messages**
❌ **Bad:**
```json
{
  "from": "user",
  "value": "<tool_call>{\"name\": \"search\", ...}</tool_call>"
}
```
Tool calls are ONLY in assistant messages. Users send tool RESULTS.

#### 7. **Overly Long or Repetitive Responses**
❌ **Bad:** 5,000-word assistant responses (model learns to be verbose)  
✅ **Good:** Concise, helpful responses (100-500 words typical)

#### 8. **Mixing Chat Templates**
Don't mix Llama3 template (`<|start_header_id|>`) with ChatML (`<|im_start|>`) — pick ONE template and use it consistently.

### 6.3 Deduplication Requirements

**Why deduplicate?**
- Duplicate examples cause overfitting
- Model memorizes instead of generalizing
- Wastes training compute

**Deduplication strategy:**

```python
import hashlib
import json

def deduplicate_sharegpt(data):
    """Removes duplicate conversations by hashing content."""
    seen_hashes = set()
    deduped = []
    
    for example in data:
        # Hash the conversation content
        content = json.dumps(example["conversations"], sort_keys=True)
        content_hash = hashlib.sha256(content.encode()).hexdigest()
        
        if content_hash not in seen_hashes:
            seen_hashes.add(content_hash)
            deduped.append(example)
    
    print(f"Removed {len(data) - len(deduped)} duplicates")
    return deduped
```

**Near-duplicate detection:**
For similar (but not identical) examples, use:
- Levenshtein distance on conversation text
- Embedding similarity (sentence-transformers)
- Remove examples with >90% similarity

### 6.4 Diversity Requirements

**Good training data has diversity in:**

1. **Input Length Distribution**
   - Short (1-50 tokens): 30%
   - Medium (50-200 tokens): 50%
   - Long (200-1000 tokens): 20%

2. **Topic Coverage**
   - Don't train only on "weather API" tool calls
   - Include: databases, email, calendar, search, calculations, file ops, etc.

3. **Response Patterns**
   - Direct answers
   - Multi-step reasoning
   - Refusals
   - Clarifying questions
   - Error handling

4. **Linguistic Variety**
   - Formal and informal language
   - Technical and non-technical topics
   - Different question phrasings
   - Various command styles

### 6.5 Manual Validation Sampling

**Validation protocol:**

1. Randomly sample 10% of training data (~500 examples for 5K dataset)
2. Manually review each sample for:
   - Correct format
   - Grounded answers (RAG)
   - Valid tool calls (if applicable)
   - Appropriate tone and style
   - No toxic/harmful content
3. If error rate >5%, investigate and fix entire dataset
4. Re-sample and validate again

**Quick validation script:**

```python
import random

def sample_for_review(data, sample_size=50):
    """Returns random sample for manual review."""
    return random.sample(data, min(sample_size, len(data)))

# Review these examples manually
review_set = sample_for_review(training_data, 50)
for i, ex in enumerate(review_set):
    print(f"\n--- Example {i+1} ---")
    print(json.dumps(ex, indent=2))
    input("Press Enter to continue...")
```

---

## 7. Training Data Generation Pipeline

### 7.1 Recommended Workflow

```
1. Dataset Selection
   ├─ Identify task priorities (tool calling > RAG > general)
   ├─ Select source datasets (Glaive, SQuAD, Dolly)
   └─ Determine target size (1K PoC, 5K production, 10K+ competitive)

2. Data Conversion
   ├─ Load source datasets
   ├─ Convert to ShareGPT format
   ├─ Adapt tool definitions to library's JSON schema format
   └─ Ensure RAG examples follow context → answer pattern

3. Data Augmentation (Optional)
   ├─ Generate synthetic examples with GPT-4/Claude for library-specific APIs
   ├─ Create negative examples (tool call refusals)
   └─ Add error handling scenarios

4. Quality Assurance
   ├─ Run structural validation (JSON, roles, format)
   ├─ Deduplicate (exact and near-duplicates)
   ├─ Check diversity (topics, lengths, patterns)
   └─ Manual review of 10% sample

5. Dataset Mixing
   ├─ Combine tool calling + RAG + general instruction
   ├─ Apply mixing ratios (50% / 30% / 20%)
   └─ Shuffle all examples

6. Train/Val/Test Split
   ├─ 80% train, 10% validation, 10% test
   ├─ Stratified split (maintain task distribution)
   └─ Save test set separately (never use during training)

7. Final Export
   ├─ Save as train.json, val.json, test.json
   ├─ Generate dataset statistics report
   └─ Document any preprocessing steps
```

### 7.2 Synthetic Data Generation (GPT-4/Claude)

For library-specific tool definitions, generate synthetic examples:

**Prompt template:**
```
You are creating training data for fine-tuning a small language model to perform tool calling.

Generate 10 diverse examples of conversations where the user requests an action, and the assistant responds by calling a tool.

Tool definition:
{
  "name": "search_documents",
  "description": "Search through indexed documents",
  "parameters": {
    "type": "object",
    "properties": {
      "query": {"type": "string", "description": "Search query"},
      "filters": {
        "type": "object",
        "properties": {
          "date_from": {"type": "string"},
          "date_to": {"type": "string"},
          "category": {"type": "string"}
        }
      }
    },
    "required": ["query"]
  }
}

Output format (ShareGPT JSON):
{
  "conversations": [
    {"from": "system", "value": "[tool definitions]"},
    {"from": "user", "value": "[user request]"},
    {"from": "assistant", "value": "<tool_call>\n{\"id\": \"call_...\", \"name\": \"...\", \"arguments\": {...}}\n</tool_call>"}
  ]
}

Requirements:
- Diverse user requests (different phrasings, intents)
- Vary complexity (simple to complex arguments)
- Include examples with and without optional parameters
- Ensure valid JSON in all tool calls
```

### 7.3 Dataset Statistics Report

After preparing the dataset, generate a report:

```python
def generate_dataset_stats(data):
    """Generates training data statistics."""
    stats = {
        "total_examples": len(data),
        "total_turns": sum(len(ex["conversations"]) for ex in data),
        "avg_turns_per_conversation": 0,
        "tool_calling_examples": 0,
        "rag_examples": 0,
        "general_examples": 0,
        "min_length": float('inf'),
        "max_length": 0,
        "avg_length": 0
    }
    
    total_length = 0
    for ex in data:
        turns = len(ex["conversations"])
        stats["avg_turns_per_conversation"] += turns
        
        # Detect example type
        content = json.dumps(ex)
        if "<tool_call>" in content:
            stats["tool_calling_examples"] += 1
        elif "Context:" in content or "context" in content.lower():
            stats["rag_examples"] += 1
        else:
            stats["general_examples"] += 1
        
        # Calculate length
        length = sum(len(msg["value"]) for msg in ex["conversations"])
        stats["min_length"] = min(stats["min_length"], length)
        stats["max_length"] = max(stats["max_length"], length)
        total_length += length
    
    stats["avg_turns_per_conversation"] /= len(data)
    stats["avg_length"] = total_length / len(data)
    
    return stats

# Example output:
# {
#   "total_examples": 5000,
#   "total_turns": 12500,
#   "avg_turns_per_conversation": 2.5,
#   "tool_calling_examples": 2500,
#   "rag_examples": 1500,
#   "general_examples": 1000,
#   "min_length": 45,
#   "max_length": 3821,
#   "avg_length": 412
# }
```

---

## 8. ONNX Compatibility Notes

### 8.1 Tokenizer Considerations

The fine-tuned model MUST use the same tokenizer as the base model. If you add special tokens (e.g., `<tool_call>`, `</tool_call>`), you must:

1. Add tokens to the tokenizer during fine-tuning
2. Resize model embeddings to match new vocabulary size
3. Save the updated tokenizer with the model
4. Use the updated tokenizer during ONNX conversion

**Example (Python):**
```python
from transformers import AutoTokenizer, AutoModelForCausalLM

tokenizer = AutoTokenizer.from_pretrained("Qwen/Qwen2.5-1.5B-Instruct")
model = AutoModelForCausalLM.from_pretrained("Qwen/Qwen2.5-1.5B-Instruct")

# Add special tokens
special_tokens = {"additional_special_tokens": ["<tool_call>", "</tool_call>"]}
tokenizer.add_special_tokens(special_tokens)
model.resize_token_embeddings(len(tokenizer))

# Fine-tune here...

# Save both
tokenizer.save_pretrained("./finetuned_model")
model.save_pretrained("./finetuned_model")
```

**Critical:** The Qwen models already have `<|im_start|>` and `<|im_end|>` in their tokenizer. Do NOT remove or modify these.

### 8.2 Chat Template Preservation

The base model's chat template (in `tokenizer_config.json`) should be preserved unless you're changing the conversation format. For Qwen models, the template is:

```jinja2
{% for message in messages %}
<|im_start|>{{ message['role'] }}
{{ message['content'] }}<|im_end|>
{% endfor %}
<|im_start|>assistant
```

If you modify this, you MUST update the template in the tokenizer config before ONNX conversion.

### 8.3 Context Length

If you train with a different `max_seq_length` than the base model:

1. Update `max_position_embeddings` in model config
2. Extend positional embeddings if necessary
3. Update `genai_config.json` after ONNX conversion to reflect new context length

**Recommendation:** Use the base model's context length (2048 for Qwen2.5-0.5B, 32768 for Qwen2.5-1.5B/3B) to avoid ONNX conversion issues.

---

## 9. Quick Start Guide

### 9.1 Creating a 1K PoC Dataset (Weekend Project)

**Goal:** Fine-tune Qwen2.5-0.5B for basic tool calling in <2 hours

**Step 1: Download Glaive dataset**
```python
from datasets import load_dataset
glaive = load_dataset("glaiveai/glaive-function-calling-v2", split="train")
```

**Step 2: Filter to 500 best examples**
```python
# Filter by relevance (e.g., weather, database, basic APIs)
filtered = glaive.filter(lambda ex: "weather" in str(ex).lower() or "database" in str(ex).lower())
tool_examples = filtered.select(range(500))
```

**Step 3: Add 300 RAG examples from SQuAD**
```python
squad = load_dataset("squad_v2", split="train").select(range(300))
rag_examples = [convert_squad_to_sharegpt(ex) for ex in squad]
```

**Step 4: Add 200 general instruction examples**
```python
dolly = load_dataset("databricks/databricks-dolly-15k", split="train").select(range(200))
general_examples = [convert_dolly_to_sharegpt(ex) for ex in dolly]
```

**Step 5: Combine and split**
```python
all_data = list(tool_examples) + rag_examples + general_examples
random.shuffle(all_data)

train = all_data[:800]
val = all_data[800:900]
test = all_data[900:1000]

# Save
import json
with open("train.json", "w") as f:
    json.dump(train, f, indent=2)
with open("val.json", "w") as f:
    json.dump(val, f, indent=2)
with open("test.json", "w") as f:
    json.dump(test, f, indent=2)
```

**Step 6: Fine-tune with Unsloth (QLoRA)**
```python
from unsloth import FastLanguageModel

model, tokenizer = FastLanguageModel.from_pretrained(
    model_name="unsloth/Qwen2.5-0.5B-Instruct",
    max_seq_length=2048,
    dtype=None,
    load_in_4bit=True
)

model = FastLanguageModel.get_peft_model(
    model,
    r=8,
    target_modules=["q_proj", "k_proj", "v_proj", "o_proj"],
    lora_alpha=16,
    lora_dropout=0,
    bias="none",
    use_gradient_checkpointing="unsloth"
)

# Train with your train.json
# (See Unsloth docs for full training code)
```

### 9.2 Validation After Training

After fine-tuning, test the model:

**Test 1: Tool calling**
```
System: [tool definitions for get_weather]
User: What's the weather in Boston?
Expected: <tool_call>{"id": "call_...", "name": "get_weather", "arguments": {"location": "Boston"}}</tool_call>
```

**Test 2: RAG grounding**
```
System: Answer only from context.
User: Context: Python was created in 1991.\n\nQuestion: When was Python created?
Expected: Python was created in 1991.
```

**Test 3: RAG refusal**
```
System: Answer only from context.
User: Context: The sky is blue.\n\nQuestion: What is quantum computing?
Expected: I don't have enough information to answer that...
```

---

## 10. Summary and Best Practices

### 10.1 Critical Success Factors

1. ✅ **Format Alignment:** Training data MUST match library's formatter output exactly
2. ✅ **Quality Over Quantity:** 5K high-quality examples > 50K noisy examples
3. ✅ **Multi-Task Mixing:** Always include general instruction data (prevents forgetting)
4. ✅ **Validation First:** Test format with 10 examples before scaling to thousands
5. ✅ **Deduplication:** Remove duplicates to prevent overfitting
6. ✅ **Test Set Isolation:** Never use test data during training or hyperparameter tuning

### 10.2 Common Pitfalls to Avoid

- ❌ Training only on tool calling (model forgets general knowledge)
- ❌ Inconsistent tool call format across examples
- ❌ RAG data that hallucinates beyond provided context
- ❌ Not validating JSON inside `<tool_call>` tags
- ❌ Missing tool result messages in multi-turn sequences
- ❌ Using datasets with incompatible licenses

### 10.3 Dataset Size Recommendations

| Use Case | Size | Training Time (QLoRA, RTX 4090) | Expected Quality |
|----------|------|----------------------------------|------------------|
| PoC / Iteration | 1,000 | 20-30 min | 60-70% tool calling accuracy |
| Production | 5,000 | 2-3 hours | 75-85% tool calling accuracy |
| Competitive | 10,000+ | 6-8 hours | 80-90% tool calling accuracy |

### 10.4 Next Steps

After creating training data:

1. **Validate format** with parsing script (Section 6.1)
2. **Run deduplication** (Section 6.3)
3. **Generate statistics** (Section 7.3)
4. **Manual review sample** (Section 6.5)
5. **Fine-tune model** (use Unsloth/LLaMA-Factory)
6. **Evaluate on test set** (tool calling accuracy, RAG F1, general benchmarks)
7. **Convert to ONNX** (merge LoRA → ONNX → quantize INT4)
8. **Test with library** (ElBruno.LocalLLMs integration tests)

---

## Appendix A: Complete Example Conversation (All Patterns)

This example demonstrates tool calling, RAG, and general instruction in a single conversation:

```json
{
  "conversations": [
    {
      "from": "system",
      "value": "You are a helpful assistant that can search documents and answer questions.\n\nYou have access to the following tools:\n\n[\n  {\n    \"type\": \"function\",\n    \"function\": {\n      \"name\": \"search_docs\",\n      \"description\": \"Search internal documentation\",\n      \"parameters\": {\n        \"type\": \"object\",\n        \"properties\": {\n          \"query\": {\"type\": \"string\"}\n        },\n        \"required\": [\"query\"]\n      }\n    }\n  }\n]\n\nWhen you need to call a tool, respond with a JSON object in this format:\n{\"name\": \"tool_name\", \"arguments\": {\"arg1\": \"value1\"}}"
    },
    {
      "from": "user",
      "value": "What's our company's vacation policy?"
    },
    {
      "from": "assistant",
      "value": "<tool_call>\n{\"id\": \"call_a1b2c3d4e5f6\", \"name\": \"search_docs\", \"arguments\": {\"query\": \"vacation policy\"}}\n</tool_call>"
    },
    {
      "from": "user",
      "value": "Tool result for call_a1b2c3d4e5f6: Found 1 document:\n\n---\n[HR Handbook - Section 4.2: Time Off]\n\nAll full-time employees are entitled to 15 days of paid vacation per year. Vacation days accrue at a rate of 1.25 days per month. Employees must submit vacation requests through the HR portal at least 2 weeks in advance. Unused vacation days do not roll over to the next year.\n---"
    },
    {
      "from": "assistant",
      "value": "According to our HR Handbook (Section 4.2), here's the vacation policy:\n\n- **Entitlement:** All full-time employees get 15 days of paid vacation per year\n- **Accrual:** Vacation days accrue at 1.25 days per month\n- **Request Process:** Submit requests through the HR portal at least 2 weeks in advance\n- **Rollover:** Unused vacation days do not carry over to the next year\n\nIs there anything specific about the vacation policy you'd like clarified?"
    },
    {
      "from": "user",
      "value": "Thanks! That's helpful. Can you also tell me what 15 times 24 equals?"
    },
    {
      "from": "assistant",
      "value": "15 times 24 equals 360."
    }
  ]
}
```

This example shows:
- System message with tool definitions
- Tool call with proper format
- Tool result provided by user
- Grounded answer from retrieved context
- General knowledge question (no tool needed)

---

**Document Version:** 1.0  
**Last Updated:** 2025-01-27  
**Maintainer:** Mouse (Fine-Tuning Specialist)
