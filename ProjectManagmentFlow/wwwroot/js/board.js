(function () {
    'use strict';

    var board = document.querySelector('[data-board]');
    if (!board) return;

    var form = document.querySelector('[data-board-move-form]');
    var announcer = document.querySelector('[data-board-announcer]');
    if (!form) return;

    var isRtl = board.dataset.rtl === 'true';
    var columns = Array.prototype.slice.call(board.querySelectorAll('[data-column-status]'));

    function statusOf(column) {
        return column.getAttribute('data-column-status');
    }

    function labelOf(column) {
        var title = column.querySelector('.ds-kanban-column__title');
        return title ? title.textContent.trim() : statusOf(column);
    }

    function announce(message) {
        if (announcer) announcer.textContent = message;
    }

    function submitMove(taskId, status, afterTaskId) {
        form.querySelector('[data-field="taskId"]').value = taskId;
        form.querySelector('[data-field="status"]').value = status;
        form.querySelector('[data-field="afterTaskId"]').value = afterTaskId || '';
        form.submit();
    }

    board.addEventListener('keydown', function (event) {
        if (!event.ctrlKey || (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight')) return;

        var card = event.target.closest('[data-task-id]');
        if (!card || card.dataset.mayMove !== 'true') return;

        var current = card.closest('[data-column-status]');
        var index = columns.indexOf(current);
        if (index < 0) return;

        var forward = isRtl ? event.key === 'ArrowLeft' : event.key === 'ArrowRight';
        var target = columns[index + (forward ? 1 : -1)];
        if (!target) {
            announce(isRtl ? 'لا يوجد عمود في هذا الاتجاه.' : 'No column in that direction.');
            return;
        }

        event.preventDefault();
        announce((isRtl ? 'نُقلت إلى ' : 'Moved to ') + labelOf(target));
        submitMove(card.getAttribute('data-task-id'), statusOf(target), null);
    });

    var dragged = null;

    board.addEventListener('dragstart', function (event) {
        var card = event.target.closest('[data-task-id]');
        if (!card || card.dataset.mayMove !== 'true') return;
        dragged = card;
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', card.getAttribute('data-task-id'));
    });

    board.addEventListener('dragend', function () {
        dragged = null;
        board.querySelectorAll('.is-drop-target').forEach(function (zone) {
            zone.classList.remove('is-drop-target');
        });
    });

    board.addEventListener('dragover', function (event) {
        var zone = event.target.closest('[data-drop-zone]');
        if (!zone || !dragged) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
        zone.classList.add('is-drop-target');
    });

    board.addEventListener('dragleave', function (event) {
        var zone = event.target.closest('[data-drop-zone]');
        if (zone && !zone.contains(event.relatedTarget)) zone.classList.remove('is-drop-target');
    });

    board.addEventListener('drop', function (event) {
        var zone = event.target.closest('[data-drop-zone]');
        if (!zone || !dragged) return;
        event.preventDefault();

        var taskId = dragged.getAttribute('data-task-id');
        var status = zone.getAttribute('data-drop-zone');
        var over = event.target.closest('[data-task-id]');
        var afterTaskId = over && over !== dragged ? over.getAttribute('data-task-id') : null;

        if (status === dragged.getAttribute('data-task-status') && !afterTaskId) {
            dragged = null;
            return;
        }

        submitMove(taskId, status, afterTaskId);
    });
})();

/* درج تفاصيل المهمّة.
   الضغط على البطاقة يجلب الجزئيّة ويعرضها في offcanvas؛ وبلا JS يبقى رابط
   البطاقة عاملاً فيفتح الصفحة الكاملة. النماذج داخل الدرج تُرسَل بـfetch
   وتُعيد رسم محتواه، فلا تُعاد الصفحة ولا يضيع موضع التمرير في اللوحة. */
(function () {
    'use strict';

    var drawer = document.querySelector('[data-task-drawer]');
    var body = document.querySelector('[data-task-drawer-body]');
    if (!drawer || !body || typeof bootstrap === 'undefined') return;

    var panel = bootstrap.Offcanvas.getOrCreateInstance(drawer);
    var current = null;

    function load(url) {
        var separator = url.indexOf('?') === -1 ? '?' : '&';
        return fetch(url + separator + 'panel=true', { headers: { 'X-Requested-With': 'fetch' } })
            .then(function (response) {
                if (!response.ok) throw new Error(response.status);
                return response.text();
            })
            .then(function (html) {
                body.innerHTML = html;
                current = url;
            });
    }

    // بعد السحب يطلق المتصفّح click على البطاقة؛ هذا العلم يمنع فتح الدرج حينها.
    var dragged = false;
    document.addEventListener('dragstart', function (event) {
        if (event.target.closest('.ds-task-card')) dragged = true;
    });
    document.addEventListener('dragend', function () {
        window.setTimeout(function () { dragged = false; }, 0);
    });

    function open(link) {
        load(link.getAttribute('href'))
            .then(function () { panel.show(); })
            .catch(function () { window.location.href = link.getAttribute('href'); });
    }

    // الضغط على أيّ موضعٍ من البطاقة يفتح التفاصيل، عدا عنصرٍ تفاعليٍّ بداخلها.
    document.addEventListener('click', function (event) {
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey) return;

        var card = event.target.closest('.ds-task-card');
        if (!card || dragged) return;
        if (event.target.closest('button, input, select, textarea, [data-drawer-form]')) return;

        var link = card.querySelector('.ds-task-card__title a');
        if (!link) return;

        event.preventDefault();
        open(link);
    });

    // البطاقة قابلةٌ للتركيز، فـEnter و Space يفتحان التفاصيل كالضغط.
    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Enter' && event.key !== ' ') return;

        var card = event.target.closest('.ds-task-card');
        if (!card || event.target !== card) return;

        var link = card.querySelector('.ds-task-card__title a');
        if (!link) return;

        event.preventDefault();
        open(link);
    });

    // نماذج الدرج: أرسِلها ثمّ أعِد رسم الدرج وحده. اللوحة خلفه تصير قديمة،
    // فتُحدَّث مرّةً واحدة عند إغلاقه بدل إعادة تحميلٍ يُغلقه بعد كلّ ضغطة.
    var dirty = false;

    body.addEventListener('submit', function (event) {
        var form = event.target.closest('[data-drawer-form]');
        if (!form || !current) return;

        event.preventDefault();
        fetch(form.getAttribute('action'), { method: 'POST', body: new FormData(form) })
            .then(function (response) {
                if (!response.ok) throw new Error(response.status);
                dirty = true;
                return load(current);
            })
            .catch(function () { form.submit(); });
    });

    // اللوحة تُحدَّث في مكانها عند إغلاق الدرج: نجلب الصفحة ونبدّل الشبكة وحدها،
    // فلا تومض ولا يضيع موضع التمرير الأفقيّ. وإن تعذّر، نعيد التحميل.
    drawer.addEventListener('hidden.bs.offcanvas', function () {
        if (!dirty) return;
        dirty = false;

        fetch(window.location.href, { headers: { 'X-Requested-With': 'fetch' } })
            .then(function (response) {
                if (!response.ok) throw new Error(response.status);
                return response.text();
            })
            .then(function (html) {
                var fresh = new DOMParser().parseFromString(html, 'text/html')
                    .querySelector('[data-board]');
                if (!fresh) throw new Error('board');

                var scroll = board.scrollLeft;
                board.innerHTML = fresh.innerHTML;
                board.scrollLeft = scroll;
            })
            .catch(function () { window.location.reload(); });
    });
}());
