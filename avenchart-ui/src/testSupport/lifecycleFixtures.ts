// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

export const lifecycleDomains = [
  "appointments",
  "messages",
  "documents",
  "encounters",
  "billing",
  "reports",
  "inventory",
] as const;

export type LifecycleDomain = (typeof lifecycleDomains)[number];
export type FixtureId = string | number;
export type FixtureCleanup = () => Promise<void>;
export type FixtureReset = () => Promise<void>;

export type LifecycleRecord = {
  domain: LifecycleDomain;
  id: FixtureId;
  cleanupMode: "delete" | "reset";
};

type CreateOptions<T> = {
  create: () => Promise<T>;
  id: (created: T) => FixtureId;
  cleanup?: (created: T) => Promise<void>;
};

/**
 * Tracks every mutation made by a browser test. Resources are removed in reverse
 * order; a deterministic database reset is used when a resource has no delete
 * contract or when explicit cleanup fails.
 */
export class LifecycleFixtureRegistry {
  readonly records: LifecycleRecord[] = [];
  private readonly cleanupActions: FixtureCleanup[] = [];
  private readonly reset?: FixtureReset;
  private resetRequired = false;

  constructor(reset?: FixtureReset) {
    this.reset = reset;
  }

  async create<T>(
    domain: LifecycleDomain,
    options: CreateOptions<T>,
  ): Promise<T> {
    const created = await options.create();
    const id = options.id(created);

    if (id === "" || id === null || id === undefined) {
      throw new Error(`${domain} fixture creation did not return a stable ID.`);
    }

    this.records.push({
      domain,
      id,
      cleanupMode: options.cleanup ? "delete" : "reset",
    });

    if (options.cleanup) {
      this.cleanupActions.push(() => options.cleanup!(created));
    } else {
      this.resetRequired = true;
    }

    return created;
  }

  appointments<T>(options: CreateOptions<T>) {
    return this.create("appointments", options);
  }

  messages<T>(options: CreateOptions<T>) {
    return this.create("messages", options);
  }

  documents<T>(options: CreateOptions<T>) {
    return this.create("documents", options);
  }

  encounters<T>(options: CreateOptions<T>) {
    return this.create("encounters", options);
  }

  billing<T>(options: CreateOptions<T>) {
    return this.create("billing", options);
  }

  reports<T>(options: CreateOptions<T>) {
    return this.create("reports", options);
  }

  inventory<T>(options: CreateOptions<T>) {
    return this.create("inventory", options);
  }

  async cleanup(): Promise<void> {
    let cleanupFailed = false;

    for (const cleanup of [...this.cleanupActions].reverse()) {
      try {
        await cleanup();
      } catch {
        cleanupFailed = true;
      }
    }

    if (this.resetRequired || cleanupFailed) {
      if (!this.reset) {
        throw new Error(
          "Fixture cleanup requires the documented clean-demo reset, but no reset handler was configured.",
        );
      }
      await this.reset();
    }
  }
}
