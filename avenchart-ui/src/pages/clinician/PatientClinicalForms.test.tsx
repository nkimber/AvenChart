// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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

  it("applies row-isolated visibility, requirement, calculation, and issues", () => {
    const repeat = normalizeClinicalFormFieldType(
      createSafeClinicalFormField(1),
      "repeat",
    );
    repeat.children = [
      {
        ...createSafeClinicalFormField(1, ""),
        key: "quantity",
        label: "Quantity",
        accessibilityLabel: "Quantity",
      },
      {
        ...createSafeClinicalFormField(2, ""),
        key: "detail",
        label: "Detail",
        accessibilityLabel: "Detail",
      },
      {
        ...normalizeClinicalFormFieldType(
          createSafeClinicalFormField(3, ""),
          "computed",
        ),
        key: "row_total",
        label: "Row total",
        accessibilityLabel: "Row total",
      },
    ];

    render(
      <FieldInput
        field={repeat}
        value={[
          { quantity: "2", detail: "", row_total: 12 },
          { quantity: "1", detail: "second", row_total: 4 },
        ]}
        required={false}
        disabled={false}
        repeatRows={[
          {
            repeatFieldKey: repeat.key,
            rowIndex: 0,
            visibleFields: {
              quantity: true,
              detail: true,
              row_total: true,
            },
            requiredFields: {
              quantity: false,
              detail: true,
              row_total: false,
            },
            issues: [
              {
                fieldKey: "detail",
                severity: "error",
                message: "Entry 1 detail is required.",
                ruleKey: null,
                repeatFieldKey: repeat.key,
                rowIndex: 0,
              },
            ],
            ruleEvaluations: [],
          },
          {
            repeatFieldKey: repeat.key,
            rowIndex: 1,
            visibleFields: {
              quantity: true,
              detail: false,
              row_total: true,
            },
            requiredFields: {
              quantity: false,
              detail: false,
              row_total: false,
            },
            issues: [],
            ruleEvaluations: [],
          },
        ]}
        onChange={vi.fn()}
      />,
    );

    expect(screen.getAllByLabelText("Detail")).toHaveLength(1);
    expect(screen.getByLabelText("Detail")).toBeRequired();
    expect(screen.getByRole("alert")).toHaveTextContent(
      "Entry 1 detail is required.",
    );
    expect(screen.getAllByText("12")).toHaveLength(1);
    expect(screen.getAllByText("4")).toHaveLength(1);
  });
});
