(function () {
    var estava = new WeakMap();
    var popoverDoMenu = new WeakMap();
    var observador = new MutationObserver(function (registros) {
        for (var i = 0; i < registros.length; i++) {
            var registro = registros[i];
            if (registro.type === "attributes")
                aoMudar(registro.target);
            else if (registro.type === "childList")
                varrer(registro.addedNodes);
        }
    });

    function aoMudar(alvo) {
        if (!alvo || alvo.nodeType !== 1 || !alvo.classList || !alvo.classList.contains("mud-menu-activator"))
            return;

        var antes = estava.get(alvo) === true;
        var agora = alvo.getAttribute("aria-expanded") === "true";
        if (!antes && agora)
            guardarPopover(alvo);
        if (antes && !agora)
            agendarDevolucao(alvo);
        estava.set(alvo, agora);
    }

    function agendarDevolucao(alvo) {
        setTimeout(function () {
            if (alvo.getAttribute("aria-expanded") === "true")
                return;
            guardarPopover(alvo);
            if (deveDevolver(document.activeElement, alvo))
                alvo.focus();
        }, 0);
    }

    function guardarPopover(alvo) {
        var popover = acharPopover(alvo);
        if (popover)
            popoverDoMenu.set(alvo, popover);
    }

    function acharPopover(alvo) {
        var controle = alvo.getAttribute("aria-controls");
        if (controle) {
            var porControle = document.getElementById(controle);
            if (porControle)
                return porControle;
        }

        var menu = alvo.closest(".mud-menu");
        if (!menu || !menu.querySelectorAll)
            return null;

        var marcadores = menu.querySelectorAll("[id^='popover-']");
        for (var i = 0; i < marcadores.length; i++) {
            var id = marcadores[i].id;
            if (id.indexOf("popover-") !== 0)
                continue;
            var conteudo = document.getElementById("popovercontent-" + id.substring(8));
            if (conteudo)
                return conteudo;
        }

        return null;
    }

    function deveDevolver(ativo, alvo) {
        if (!ativo || ativo === document.body || ativo === document.documentElement)
            return true;
        if (dentroDoMenu(ativo, alvo))
            return true;
        return false;
    }

    function dentroDoMenu(ativo, alvo) {
        if (ativo === alvo || alvo.contains(ativo))
            return true;
        var popover = popoverDoMenu.get(alvo);
        if (popover && (ativo === popover || popover.contains(ativo)))
            return true;
        return false;
    }

    function lembrar(alvo) {
        if (!estava.has(alvo))
            estava.set(alvo, alvo.getAttribute("aria-expanded") === "true");
    }

    function varrer(nos) {
        for (var i = 0; i < nos.length; i++) {
            var no = nos[i];
            if (!no || no.nodeType !== 1)
                continue;
            if (no.classList && no.classList.contains("mud-menu-activator"))
                lembrar(no);
            if (no.querySelectorAll)
                varrer(no.querySelectorAll(".mud-menu-activator"));
        }
    }

    observador.observe(document.documentElement, {
        attributes: true,
        attributeFilter: ["aria-expanded"],
        childList: true,
        subtree: true
    });
    varrer(document.querySelectorAll(".mud-menu-activator"));
})();
