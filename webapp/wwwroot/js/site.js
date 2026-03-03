// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
window.chipTimeouts = {};
window.updateAvailableChips = function(inputId, containerId, url, dataAttr, level = null) {
    const input = document.getElementById(inputId);
    const container = document.getElementById(containerId);
    if (!input || !container) return;

    const val = input.value.trim();
    
    clearTimeout(window.chipTimeouts[inputId]);
    window.chipTimeouts[inputId] = setTimeout(() => {
        fetch(`${url}?term=${encodeURIComponent(val)}`)
            .then(r => r.json())
            .then(data => {
                container.innerHTML = '';
                // Limit to 5
                data.slice(0, 5).forEach(item => {
                    const chip = document.createElement('span');
                    chip.className = 'available-chip';
                    chip.setAttribute(`data-${dataAttr}`, item);
                    if (level) chip.setAttribute('data-level', level);
                    chip.textContent = item;
                    chip.addEventListener('click', () => {
                        input.value = item;
                        input.dispatchEvent(new Event('change'));
                    });
                    container.appendChild(chip);
                });
            });
    }, 300);
};
