// صفحة «الهيكل». بلا JS يبقى كلّ شيء عاملًا: الشجرة روابط، النماذج ترسل عاديًّا،
// والمبدّلات أزرار تُرجِل الصفحة. JS هنا طبقة تجربة — تقريب وطيّ وبحث — لا منطق.
(function () {
    "use strict";

    var page = document.querySelector("[data-structure]");
    if (!page) return;

    // ── مبدّل القائمة/المخطّط ────────────────────────────────────────────
    function queryString(params) {
        var current = new URLSearchParams(window.location.search);
        if (params.unit !== undefined) current.set("unit", params.unit);
        if (params.view !== undefined) current.set("view", params.view);
        if (params.tab !== undefined) current.set("tab", params.tab);
        return current.toString();
    }

    page.querySelectorAll("[data-structure-view]").forEach(function (button) {
        button.addEventListener("click", function () {
            var unit = new URLSearchParams(window.location.search).get("unit");
            var target = button.getAttribute("data-structure-view");
            if (window.location.pathname + "?" + queryString({ view: target, tab: "overview" })
                    === window.location.pathname + window.location.search) {
                return;
            }
            window.location.replace(window.location.pathname + "?" + queryString({ view: target, tab: "overview" }));
        });
    });

    // ── درج الشجرة الجانبية ─────────────────────────────────────────────
    var drawer = page.querySelector("[data-structure-tree-panel]");
    var scrim = page.querySelector(".ds-org-structure__scrim");
    function setDrawer(open) {
        if (drawer) drawer.classList.toggle("is-open", open);
        if (scrim) scrim.classList.toggle("is-open", open);
    }
    page.querySelectorAll("[data-structure-tree-open]").forEach(function (button) {
        button.addEventListener("click", function () { setDrawer(true); });
    });
    page.querySelectorAll("[data-structure-tree-close]").forEach(function (button) {
        button.addEventListener("click", function () { setDrawer(false); });
    });

    // ── الشجرة: تجميع/طيّ وبحث ─────────────────────────────────────────
    var tree = page.querySelector("[data-structure-tree]");
    if (tree) {
        // الشجرة مسطّحة، فالطيّ إخفاءُ ذرّيّةٍ لا إخفاءُ وعاء. نبني خريطة
        // الأب←الأبناء مرّةً، ثمّ نطوي بالنزول فيها.
        var items = Array.prototype.slice.call(tree.querySelectorAll("[data-structure-node]"));
        var childrenOf = {};
        items.forEach(function (item) {
            var parent = item.getAttribute("data-node-parent");
            if (!parent) return;
            (childrenOf[parent] = childrenOf[parent] || []).push(item);
        });

        var collapsed = {};

        function descendants(id) {
            var out = [];
            (childrenOf[id] || []).forEach(function (child) {
                out.push(child);
                out = out.concat(descendants(child.getAttribute("data-node-id")));
            });
            return out;
        }

        function paint() {
            items.forEach(function (item) { item.hidden = false; });
            Object.keys(collapsed).forEach(function (id) {
                if (!collapsed[id]) return;
                descendants(id).forEach(function (item) { item.hidden = true; });
            });
            items.forEach(function (item) {
                var id = item.getAttribute("data-node-id");
                var button = item.querySelector("[data-structure-toggle]");
                if (!button) return;
                var isCollapsed = Boolean(collapsed[id]);
                button.textContent = isCollapsed ? "+" : "\u2212";
                item.setAttribute("aria-expanded", isCollapsed ? "false" : "true");
            });
        }

        tree.addEventListener("click", function (event) {
            var button = event.target.closest("[data-structure-toggle]");
            if (!button) return;
            var id = button.getAttribute("data-structure-toggle");
            collapsed[id] = !collapsed[id];
            paint();
        });

        var treeExpand = page.querySelector('[data-structure-action="expand-all"]');
        var treeCollapse = page.querySelector('[data-structure-action="collapse-all"]');
        if (treeExpand) {
            treeExpand.addEventListener("click", function () {
                collapsed = {};
                paint();
            });
        }
        if (treeCollapse) {
            treeCollapse.addEventListener("click", function () {
                // الجذر يبقى مكشوفًا — طيّ الكلّ لا يخفي المنطلق.
                collapsed = {};
                items.forEach(function (item) {
                    if (item.getAttribute("data-node-parent")) return;
                    collapsed[item.getAttribute("data-node-id")] = true;
                });
                paint();
            });
        }

        var search = page.querySelector("[data-structure-search]");
        if (search) {
            search.addEventListener("input", function () {
                var needle = search.value.trim().toLowerCase();
                if (needle === "") {
                    collapsed = {};
                    paint();
                    return;
                }

                // المطابِق يظهر ومعه سلسلة آبائه، كي لا تُقطع العقدة عن موضعها.
                var keep = {};
                items.forEach(function (item) {
                    var name = (item.getAttribute("data-node-name") || "").toLowerCase();
                    if (name.indexOf(needle) === -1) return;
                    var cursor = item;
                    while (cursor) {
                        keep[cursor.getAttribute("data-node-id")] = true;
                        var parentId = cursor.getAttribute("data-node-parent");
                        cursor = parentId
                            ? tree.querySelector('[data-node-id="' + parentId + '"]')
                            : null;
                    }
                });

                items.forEach(function (item) {
                    item.hidden = !keep[item.getAttribute("data-node-id")];
                });
            });
        }

        paint();
    }

    // ── المخطّط: تقارب، طيّ، بحث، ملاءمة العرض ────────────────────────
    var chart = page.querySelector("[data-org-chart]");
    if (!chart) return;

    var canvas = chart.querySelector("[data-org-chart-canvas]");
    var viewport = chart.querySelector("[data-org-chart-viewport]");
    var zoomOutput = chart.querySelector("[data-org-chart-zoom-output]");
    var zoom = 1;

    function applyZoom() {
        if (canvas) canvas.style.zoom = String(zoom);
        if (zoomOutput) zoomOutput.textContent = Math.round(zoom * 100) + "%";
    }

    chart.querySelectorAll("[data-org-chart-action]").forEach(function (button) {
        button.addEventListener("click", function () {
            var action = button.getAttribute("data-org-chart-action");
            if (action === "zoom-in") zoom = Math.min(2, zoom + 0.1);
            else if (action === "zoom-out") zoom = Math.max(0.4, zoom - 0.1);
            else if (action === "expand-all") {
                chart.querySelectorAll("[data-org-chart-children]").forEach(function (c) { c.removeAttribute("hidden"); });
                chart.querySelectorAll("[data-org-chart-toggle]").forEach(function (t) {
                    t.setAttribute("aria-expanded", "true"); t.textContent = "−";
                });
                return;
            }
            else if (action === "collapse-all") {
                chart.querySelectorAll("[data-org-chart-children]").forEach(function (c) { c.setAttribute("hidden", ""); });
                chart.querySelectorAll("[data-org-chart-toggle]").forEach(function (t) {
                    t.setAttribute("aria-expanded", "false"); t.textContent = "+";
                });
                return;
            }
            else if (action === "fit") {
                if (canvas && viewport) {
                    canvas.style.zoom = "1";
                    var natural = canvas.scrollWidth;
                    if (natural > 0) {
                        zoom = Math.min(2, viewport.clientWidth / natural);
                    }
                }
            }
            applyZoom();
        });
    });

    chart.querySelectorAll("[data-org-chart-toggle]").forEach(function (button) {
        button.addEventListener("click", function () {
            var branch = button.closest("[data-org-chart-branch]");
            var children = branch ? branch.querySelector("[data-org-chart-children]") : null;
            if (!children) return;
            var open = children.hasAttribute("hidden");
            if (open) children.removeAttribute("hidden"); else children.setAttribute("hidden", "");
            button.setAttribute("aria-expanded", open ? "true" : "false");
            button.textContent = open ? "−" : "+";
        });
    });

    var chartSearch = chart.querySelector("[data-org-chart-search]");
    if (chartSearch) {
        chartSearch.addEventListener("input", function () {
            var needle = chartSearch.value.trim().toLowerCase();
            chart.querySelectorAll("[data-org-chart-branch]").forEach(function (branch) {
                var name = (branch.getAttribute("data-node-name") || "").toLowerCase();
                var match = needle === "" || name.indexOf(needle) !== -1;
                branch.classList.toggle("is-search-match", needle !== "" && match);
            });
        });
    }

    applyZoom();
})();
