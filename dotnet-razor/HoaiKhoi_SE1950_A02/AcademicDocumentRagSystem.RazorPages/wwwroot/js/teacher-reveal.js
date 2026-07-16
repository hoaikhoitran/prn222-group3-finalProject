(() => {
    if (!document.body.classList.contains('teacher-area')) return;

    const page = document.querySelector('.page-body > .edurag-page');
    const chatGrid = document.querySelector('.page-body > .chat-grid');
    const blocks = page ? [...page.children] : (chatGrid ? [chatGrid] : []);
    const revealEntries = [];
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    blocks.forEach((block, blockIndex) => {
        let components = [];

        if (block.matches('.row')) {
            components = [...block.children].filter((child) => child.className.includes('col-'));
        } else if (block.id === 'course-list') {
            components = [...block.querySelectorAll(':scope > .row > [class*="col-"]')];
        } else if (block.matches('.chat-grid')) {
            components = [...block.children];
        }

        if (components.length > 0) {
            block.classList.add('teacher-reveal-container');
            components.forEach((component, componentIndex) => {
                revealEntries.push({
                    item: component,
                    delay: Math.min(componentIndex, 6) * 55
                });
            });
            return;
        }

        revealEntries.push({
            item: block,
            delay: Math.min(blockIndex, 4) * 55
        });
    });

    const revealItems = revealEntries.map((entry) => entry.item);
    revealEntries.forEach(({ item, delay }) => {
        item.classList.add('teacher-reveal-item');
        item.style.setProperty('--teacher-reveal-delay', `${delay}ms`);
    });

    if (reduceMotion || !('IntersectionObserver' in window)) {
        revealItems.forEach((item) => item.classList.add('is-teacher-visible'));
        return;
    }

    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (!entry.isIntersecting) return;

            entry.target.classList.add('is-teacher-visible');
            observer.unobserve(entry.target);
        });
    }, {
        threshold: 0.08,
        rootMargin: '0px 0px -4% 0px'
    });

    revealItems.forEach((item) => observer.observe(item));
})();
