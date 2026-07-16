(() => {
    const form = document.querySelector('[data-contact-form]');
    if (!form) return;

    form.addEventListener('submit', (event) => {
        event.preventDefault();

        const data = new FormData(form);
        const subject = String(data.get('subject') || '').trim();
        const body = [
            `Họ và tên: ${String(data.get('name') || '').trim()}`,
            `Email: ${String(data.get('email') || '').trim()}`,
            '',
            String(data.get('message') || '').trim()
        ].join('\n');

        window.location.href = `mailto:edurag@university.edu.vn?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
    });
})();
