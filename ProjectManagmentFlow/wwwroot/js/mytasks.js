// نافذة «مهمة جديدة»: الوجه الشخصيّ يُقفل حقول المشروع بدل إخفائها،
// فالحقل المقفل لا يُرسَل — والخادم لا يعتمد على ذلك بل يحرس بنفسه.
(function () {
    "use strict";

    const form = document.querySelector("[data-new-task]");
    if (!form) return;

    const kinds = form.querySelectorAll('input[name="kind"]');
    const projectFields = form.querySelectorAll("[data-project-field]");
    const visibility = form.querySelectorAll("[data-visibility]");
    const lock = form.querySelector("[data-visibility-lock]");
    const personalNote = form.querySelector("[data-personal-note]");
    const hints = form.querySelectorAll("[data-project-hint][data-personal-hint]");
    const project = form.querySelector("#nt-project");
    const assignee = form.querySelector("#nt-assignee");
    const personalOption = form.querySelector("[data-personal-option]");
    const teamOption = form.querySelector("[data-team-option]");
    const hasProjects = Boolean(project && project.querySelector("option[value]:not([value=''])"));

    // ما اختاره المستخدم فعلاً لوجه الفريق. الوجه الشخصيّ يفرض «خاصّة»،
    // والرجوع بلا هذه الذاكرة يُبقيها مفروضةً فتُحجب مهمّة فريقٍ عن فريقها.
    let chosen = form.querySelector("[data-visibility]:checked")?.value ?? "project";

    const isTeamTask = () =>
        hasProjects && form.querySelector('input[name="kind"]:checked')?.value === "project";

    const syncKind = () => {
        const team = isTeamTask();

        projectFields.forEach((field) => { field.disabled = !team; });

        visibility.forEach((radio) => {
            radio.disabled = !team;
            // بلا مشروع لا فريق يراها: «خاصّة» إلزاماً.
            radio.checked = radio.value === (team ? chosen : "private");
        });

        if (lock) lock.hidden = team;
        if (personalNote) personalNote.hidden = team;

        // الخيار المخفيّ يبقى ظاهراً ما دام مختاراً؛ فالتبديل نقلٌ للاختيار لا إخفاءٌ فقط.
        if (personalOption) personalOption.hidden = personalOption.disabled = team;
        if (teamOption) teamOption.hidden = teamOption.disabled = !team;
        const wanted = team ? teamOption : personalOption;
        if (assignee && wanted && !assignee.selectedOptions[0]?.dataset.project) {
            assignee.selectedIndex = [...assignee.options].indexOf(wanted);
        }

        hints.forEach((hint) => {
            hint.textContent = team ? hint.dataset.projectHint : hint.dataset.personalHint;
        });
    };

    // كلّ مشروعٍ وأعضاؤه: عرض غيرهم يُغري بإسنادٍ يرفضه الخادم.
    const syncAssignee = () => {
        if (!project || !assignee) return;
        const selected = project.value;
        const current = assignee.selectedOptions[0];

        assignee.querySelectorAll("optgroup").forEach((group) => {
            const mine = group.dataset.project === selected;
            group.hidden = !mine;
            group.disabled = !mine;
        });

        if (current?.dataset.project && current.dataset.project !== selected) {
            assignee.value = "";
        }
    };

    visibility.forEach((radio) => radio.addEventListener("change", () => {
        if (radio.checked && isTeamTask()) chosen = radio.value;
    }));

    kinds.forEach((radio) => radio.addEventListener("change", syncKind));
    project?.addEventListener("change", syncAssignee);

    syncAssignee();
    syncKind();
})();

/* درج تفاصيل المهمّة في «مهامي».
   الضغط على الصفّ يجلب الجزئيّة ويعرضها في offcanvas؛ وبلا JS يبقى رابط
   العنوان عاملاً. نماذج الدرج تُرسَل بـfetch وتُعيد رسم محتواه وحده، ثمّ
   تُحدَّث القائمة مرّةً واحدة عند الإغلاق بدل إعادة تحميلٍ بعد كلّ ضغطة. */
(function () {
    "use strict";

    const drawer = document.querySelector("[data-task-drawer]");
    const body = document.querySelector("[data-task-drawer-body]");
    const list = document.querySelector("[data-my-tasks]");
    if (!drawer || !body || !list || typeof bootstrap === "undefined") return;

    const panel = bootstrap.Offcanvas.getOrCreateInstance(drawer);
    let current = null;
    let dirty = false;

    const load = (url) =>
        fetch(url + (url.includes("?") ? "&" : "?") + "panel=true",
            { headers: { "X-Requested-With": "fetch" } })
            .then((response) => {
                if (!response.ok) throw new Error(response.status);
                return response.text();
            })
            .then((html) => { body.innerHTML = html; current = url; });

    const open = (link) => {
        const href = link.getAttribute("href");
        load(href).then(() => panel.show()).catch(() => { window.location.href = href; });
    };

    // الصفّ كلّه يفتح الدرج — عدا ما له عملُه: قائمة الحالة، ورابط المشروع.
    list.addEventListener("click", (event) => {
        const row = event.target.closest(".ds-task-row");
        if (!row) return;
        if (event.target.closest("button, input, select, textarea, form")) return;

        const link = row.querySelector(".ds-task-row__title a");
        if (!link) return;

        event.preventDefault();
        open(link);
    });

    body.addEventListener("submit", (event) => {
        const form = event.target.closest("[data-drawer-form]");
        if (!form || !current) return;

        event.preventDefault();
        fetch(form.getAttribute("action"), { method: "POST", body: new FormData(form) })
            .then((response) => {
                if (!response.ok) throw new Error(response.status);
                dirty = true;
                return load(current);
            })
            .catch(() => form.submit());
    });

    drawer.addEventListener("hidden.bs.offcanvas", () => {
        if (!dirty) return;
        dirty = false;

        fetch(window.location.href, { headers: { "X-Requested-With": "fetch" } })
            .then((response) => {
                if (!response.ok) throw new Error(response.status);
                return response.text();
            })
            .then((html) => {
                const fresh = new DOMParser().parseFromString(html, "text/html")
                    .querySelector("[data-my-tasks]");
                if (!fresh) throw new Error("list");
                list.innerHTML = fresh.innerHTML;
            })
            .catch(() => window.location.reload());
    });
}());
