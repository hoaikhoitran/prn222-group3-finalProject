(() => {
    const revealItems = [...document.querySelectorAll('[data-home-reveal]')];
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    document.querySelectorAll('[data-home-stagger]').forEach((group) => {
        [...group.children]
            .filter((child) => child.hasAttribute('data-home-reveal'))
            .forEach((child, index) => {
                child.style.setProperty('--home-reveal-delay', `${index * 70}ms`);
            });
    });

    if (reduceMotion || !('IntersectionObserver' in window)) {
        revealItems.forEach((item) => item.classList.add('is-home-visible'));
        return;
    }

    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (!entry.isIntersecting) return;

            entry.target.classList.add('is-home-visible');
            observer.unobserve(entry.target);
        });
    }, {
        threshold: 0.12,
        rootMargin: '0px 0px -6% 0px'
    });

    revealItems.forEach((item) => observer.observe(item));
})();
