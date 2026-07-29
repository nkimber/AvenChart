import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import {
  createSafeClinicalFormField,
  normalizeClinicalFormFieldType,
} from "../../domain/clinicalFormRepeatAuthoring.ts";
import { FieldInput } from "./PatientClinicalForms.tsx";

describe("patient bounded repeat input", () => {
  it("enforces row controls and gives repeated child inputs unique IDs", () => {
    const repeat = normalizeClinicalFormFieldType(
      createSafeClinicalFormField(1),
      "repeat",
    );
    repeat.repeatMinimum = 1;
    repeat.repeatMaximum = 2;
    repeat.children[0] = {
      ...repeat.children[0]!,
      key: "note",
      label: "Row note",
      accessibilityLabel: "Row note",
      required: true,
    };
    const onChange = vi.fn();
    const firstRow = [{ note: "First" }];
    const { rerender } = render(
      <FieldInput
        field={repeat}
        value={firstRow}
        required={false}
        disabled={false}
        onChange={onChange}
      />,
    );

    expect(screen.getByRole("button", { name: "Remove entry" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Add entry" }));
    expect(onChange).toHaveBeenLastCalledWith([{ note: "First" }, {}]);

    rerender(
      <FieldInput
        field={repeat}
        value={[{ note: "First" }, { note: "Second" }]}
        required={false}
        disabled={false}
        onChange={onChange}
      />,
    );

    const childInputs = screen.getAllByRole("textbox", { name: "Row note" });
    expect(childInputs).toHaveLength(2);
    expect(childInputs[0]?.id).not.toBe(childInputs[1]?.id);
    expect(
      screen.getByRole("button", { name: "Add entry" }),
    ).toBeDisabled();
    expect(
      screen.getAllByRole("button", { name: "Remove entry" })[0],
    ).toBeEnabled();
  });
});
