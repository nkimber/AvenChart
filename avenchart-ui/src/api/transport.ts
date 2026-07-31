// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

export const apiBaseUrl =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5001";

export const SESSION_INVALID_EVENT = "avenchart-ui:session-invalid";

export type SessionScope = "clinician" | "portal";

export type ApiProblemDetails = {
  title?: string;
  detail?: string;
  error?: string;
  status?: number;
  errors?: Record<string, string[]>;
  traceId?: string;
};

export type ApiErrorKind = "http" | "network" | "timeout" | "cancelled";

export class ApiRequestError extends Error {
  readonly status?: number;
  readonly problem?: ApiProblemDetails;
  readonly kind: ApiErrorKind;

  constructor(
    message: string,
    status?: number,
    problem?: ApiProblemDetails,
    kind: ApiErrorKind = "http",
  ) {
    super(message);
    this.name = "ApiRequestError";
    this.status = status;
    this.problem = problem;
    this.kind = kind;
  }
}

const API_TIMEOUT_MILLISECONDS = 30_000;

function announceInvalidSession(scope: SessionScope) {
  if (typeof window === "undefined") return;
  window.dispatchEvent(
    new CustomEvent(SESSION_INVALID_EVENT, { detail: { scope } }),
  );
}

async function parseProblemDetails(
  response: Response,
): Promise<ApiProblemDetails | undefined> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) return undefined;

  try {
    return (await response.json()) as ApiProblemDetails;
  } catch {
    return undefined;
  }
}

export async function requireSuccessfulResponse(
  response: Response,
  action: string,
  scope?: SessionScope,
): Promise<Response> {
  if (response.ok) return response;
  if (response.status === 401 && scope) announceInvalidSession(scope);

  const problem = await parseProblemDetails(response);
  const message =
    problem?.detail ??
    problem?.error ??
    problem?.title ??
    `${action} failed with ${response.status}`;
  throw new ApiRequestError(message, response.status, problem);
}

/**
 * Governed transport for every application request. It adds a bounded
 * timeout, preserves caller cancellation, identifies the protected session
 * scope from its header, and normalizes HTTP/network failures.
 */
export async function apiFetch(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<Response> {
  const requestController = new AbortController();
  const callerSignal = init.signal;
  const headers = new Headers(init.headers);
  const scope: SessionScope | undefined = headers.has(
    "X-Legacy EHR-Patient-Portal-Session",
  )
    ? "portal"
    : headers.has("X-Legacy EHR-Session")
      ? "clinician"
      : undefined;
  const action = `${init.method ?? "GET"} ${String(input).replace(apiBaseUrl, "")}`;
  let timedOut = false;

  const cancelFromCaller = () => requestController.abort(callerSignal?.reason);
  if (callerSignal?.aborted) {
    cancelFromCaller();
  } else {
    callerSignal?.addEventListener("abort", cancelFromCaller, { once: true });
  }

  const timeout = globalThis.setTimeout(() => {
    timedOut = true;
    requestController.abort();
  }, API_TIMEOUT_MILLISECONDS);

  try {
    const response = await globalThis.fetch(input, {
      ...init,
      signal: requestController.signal,
    });
    await requireSuccessfulResponse(response, action, scope);
    return response;
  } catch (caught) {
    if (caught instanceof ApiRequestError) throw caught;
    if (callerSignal?.aborted) {
      throw new ApiRequestError(
        `${action} was cancelled.`,
        undefined,
        undefined,
        "cancelled",
      );
    }
    if (timedOut) {
      throw new ApiRequestError(
        `${action} timed out. Try again.`,
        undefined,
        undefined,
        "timeout",
      );
    }
    throw new ApiRequestError(
      `${action} could not reach the server. Check your connection and try again.`,
      undefined,
      undefined,
      "network",
    );
  } finally {
    globalThis.clearTimeout(timeout);
    callerSignal?.removeEventListener("abort", cancelFromCaller);
  }
}

export function isInvalidSessionError(error: unknown): boolean {
  return (
    error instanceof ApiRequestError &&
    (error.status === 401 || error.status === 403)
  );
}

export function isRequestCancellation(error: unknown): boolean {
  return (
    (error instanceof DOMException && error.name === "AbortError") ||
    (error instanceof ApiRequestError && error.kind === "cancelled")
  );
}
