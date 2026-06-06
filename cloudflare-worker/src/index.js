const json = (body, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8"
    }
  });

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (request.method === "GET" && url.pathname === "/health") {
      return json({
        success: true,
        status: "healthy"
      });
    }

    if (request.method === "POST" && url.pathname === "/ai/chat") {
      return handleChat(request, env);
    }

    return json({
      success: false,
      error: "Not found"
    }, 404);
  }
};

async function handleChat(request, env) {
  const ollamaBaseUrl = env.OLLAMA_BASE_URL;
  const model = env.OLLAMA_MODEL || "llama3.1";

  if (!ollamaBaseUrl) {
    return json({
      success: false,
      error: "OLLAMA_BASE_URL is not configured"
    }, 500);
  }

  let body;
  try {
    body = await request.json();
  } catch {
    return json({
      success: false,
      error: "Request body must be valid JSON"
    }, 400);
  }

  const message = typeof body.message === "string" ? body.message.trim() : "";
  if (!message) {
    return json({
      success: false,
      error: "message is required"
    }, 400);
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 30000);

  try {
    const ollamaResponse = await fetch(`${ollamaBaseUrl.replace(/\/$/, "")}/api/generate`, {
      method: "POST",
      headers: {
        "content-type": "application/json"
      },
      signal: controller.signal,
      body: JSON.stringify({
        model,
        prompt: message,
        stream: false
      })
    });

    if (!ollamaResponse.ok) {
      const errorText = await ollamaResponse.text();

      return json({
        success: false,
        error: "Ollama request failed",
        status: ollamaResponse.status,
        detail: safeErrorDetail(errorText)
      }, 502);
    }

    const ollamaJson = await ollamaResponse.json();

    return json({
      success: true,
      model,
      response: ollamaJson.response ?? "",
      raw: ollamaJson
    });
  } catch (error) {
    if (error?.name === "AbortError") {
      return json({
        success: false,
        error: "Ollama request timed out"
      }, 504);
    }

    return json({
      success: false,
      error: "Ollama is unavailable"
    }, 502);
  } finally {
    clearTimeout(timeout);
  }
}

function safeErrorDetail(text) {
  if (!text) {
    return "";
  }

  return text.slice(0, 500);
}
