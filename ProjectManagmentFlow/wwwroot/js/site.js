(function () {
    "use strict";

    document.querySelectorAll("[data-permission-matrix]").forEach((matrix) => {
        const rows = Array.from(matrix.querySelectorAll("[data-permission-row]"));
        const columnToggles = Array.from(matrix.querySelectorAll("[data-permission-column]"));
        const workspace = matrix.closest(".ds-permission-workspace") ?? matrix.parentElement;
        const previewCount = workspace?.querySelector("[data-permission-count]");

        const isGranted = (control) => control.getAttribute("aria-checked") === "true";

        const setState = (control, state) => {
            control.classList.remove("is-granted", "is-denied", "is-mixed");
            control.classList.add(`is-${state}`);
            control.setAttribute("aria-checked", state === "mixed" ? "mixed" : String(state === "granted"));
            control.textContent = state === "granted" ? "✓" : state === "mixed" ? "−" : "";
        };

        const stateFor = (controls) => {
            const grantedCount = controls.filter(isGranted).length;
            if (grantedCount === 0) return "denied";
            if (grantedCount === controls.length) return "granted";
            return "mixed";
        };

        const updateSummary = () => {
            rows.forEach((row) => {
                const cells = Array.from(row.querySelectorAll("[data-permission-cell]"));
                const rowToggle = row.querySelector("[data-permission-row-toggle]");
                const fraction = row.querySelector(".ds-permission-service__count");
                if (rowToggle && cells.length > 0) setState(rowToggle, stateFor(cells));
                if (fraction) fraction.textContent = `${cells.filter(isGranted).length}/${cells.length}`;
                row.classList.toggle("has-grants", cells.some(isGranted));
            });

            columnToggles.forEach((columnToggle) => {
                const operation = columnToggle.dataset.permissionColumn;
                const cells = rows
                    .flatMap((row) => Array.from(row.querySelectorAll("[data-permission-cell]")))
                    .filter((cell) => cell.dataset.operation === operation);
                if (cells.length > 0) setState(columnToggle, stateFor(cells));
            });

            if (previewCount) {
                previewCount.textContent = String(
                    matrix.querySelectorAll('[data-permission-cell][aria-checked="true"]').length);
            }
        };

        matrix.querySelectorAll("[data-permission-cell]").forEach((cell) => {
            cell.addEventListener("click", () => {
                setState(cell, isGranted(cell) ? "denied" : "granted");
                updateSummary();
            });
        });

        rows.forEach((row) => {
            const rowToggle = row.querySelector("[data-permission-row-toggle]");
            if (!rowToggle) return;
            rowToggle.addEventListener("click", () => {
                const targetState = isGranted(rowToggle) ? "denied" : "granted";
                row.querySelectorAll("[data-permission-cell]").forEach((cell) => setState(cell, targetState));
                updateSummary();
            });
        });

        columnToggles.forEach((columnToggle) => {
            columnToggle.addEventListener("click", () => {
                const targetState = isGranted(columnToggle) ? "denied" : "granted";
                const operation = columnToggle.dataset.permissionColumn;
                rows.forEach((row) => {
                    row.querySelectorAll("[data-permission-cell]").forEach((cell) => {
                        if (cell.dataset.operation === operation) setState(cell, targetState);
                    });
                });
                updateSummary();
            });
        });

        updateSummary();
    });

    document.querySelectorAll("[data-permission-form]").forEach((form) => {
        form.addEventListener("submit", () => {
            form.querySelectorAll("[data-permission-cell]").forEach((cell) => {
                const input = cell.parentElement?.querySelector("[data-permission-input]");
                if (input) input.checked = cell.getAttribute("aria-checked") === "true";
            });
        });

        form.addEventListener("reset", () => {
            window.requestAnimationFrame(() => window.location.reload());
        });
    });
})();
