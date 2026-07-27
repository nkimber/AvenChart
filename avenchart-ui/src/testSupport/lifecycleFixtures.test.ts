import { describe, expect, it, vi } from "vitest";
import {
  LifecycleFixtureRegistry,
  lifecycleDomains,
  type LifecycleDomain,
} from "./lifecycleFixtures.ts";

describe("LifecycleFixtureRegistry", () => {
  it.each(lifecycleDomains)(
    "records and cleans a %s fixture by stable ID",
    async (domain) => {
      const cleanup = vi.fn(async () => undefined);
      const registry = new LifecycleFixtureRegistry();

      await registry[domain]({
        create: async () => ({ id: `${domain}-fixture` }),
        id: (created) => created.id,
        cleanup,
      });
      await registry.cleanup();

      expect(registry.records).toEqual([
        { domain, id: `${domain}-fixture`, cleanupMode: "delete" },
      ]);
      expect(cleanup).toHaveBeenCalledOnce();
    },
  );

  it("cleans related resources in reverse creation order", async () => {
    const order: LifecycleDomain[] = [];
    const registry = new LifecycleFixtureRegistry();

    await registry.appointments({
      create: async () => ({ id: 1 }),
      id: ({ id }) => id,
      cleanup: async () => {
        order.push("appointments");
      },
    });
    await registry.messages({
      create: async () => ({ id: 2 }),
      id: ({ id }) => id,
      cleanup: async () => {
        order.push("messages");
      },
    });

    await registry.cleanup();
    expect(order).toEqual(["messages", "appointments"]);
  });

  it("invokes the clean-demo reset for resources without delete contracts", async () => {
    const reset = vi.fn(async () => undefined);
    const registry = new LifecycleFixtureRegistry(reset);

    await registry.inventory({
      create: async () => ({ transactionId: 44 }),
      id: ({ transactionId }) => transactionId,
    });
    await registry.cleanup();

    expect(registry.records[0]).toMatchObject({ cleanupMode: "reset" });
    expect(reset).toHaveBeenCalledOnce();
  });

  it("falls back to reset if a delete cleanup fails", async () => {
    const reset = vi.fn(async () => undefined);
    const registry = new LifecycleFixtureRegistry(reset);

    await registry.documents({
      create: async () => ({ id: "document-1" }),
      id: ({ id }) => id,
      cleanup: async () => {
        throw new Error("delete unavailable");
      },
    });
    await registry.cleanup();

    expect(reset).toHaveBeenCalledOnce();
  });
});
