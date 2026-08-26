// إزالة العضو قرارٌ لا يُتراجع عنه، فيُسأل عنه قبل الإرسال.
// بلا JS يبقى الإرسال عاملاً — التأكيد تحسينٌ لا حارس؛ الحارس في الخادم.
(function () {
    "use strict";

    document.addEventListener("submit", (event) => {
        const form = event.target.closest("[data-confirm]");
        if (!form) return;
        if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    });
}());
