(function () {
    var estava = new WeakMap();
    var popoverDoMenu = new WeakMap();
    var popoversDoMenu = [];
    var tabDirecao = 1;
    var tabQuando = 0;
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
            if (deveDevolver(document.activeElement, alvo)) {
                if ((Date.now() - tabQuando) < 500)
                    moverParaFora(alvo, tabDirecao);
                else
                    alvo.focus();
            }
            tabQuando = 0;
        }, 0);
    }

    function aoTeclar(evento) {
        if (!evento || evento.key !== "Tab")
            return;
        var origem = evento.target;
        if (!origem || !origemDoMenu(origem))
            return;
        tabDirecao = evento.shiftKey ? -1 : 1;
        tabQuando = Date.now();
    }

    function origemDoMenu(origem) {
        for (var i = 0; i < popoversDoMenu.length; i++) {
            var popover = popoversDoMenu[i];
            if (popover && (popover === origem || popover.contains(origem)))
                return true;
        }
        return false;
    }

    function guardarPopover(alvo) {
        var popover = acharPopover(alvo);
        if (!popover)
            return;
        popoverDoMenu.set(alvo, popover);
        for (var i = 0; i < popoversDoMenu.length; i++) {
            if (popoversDoMenu[i] === popover)
                return;
        }
        popoversDoMenu.push(popover);
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

    function moverParaFora(alvo, direcao) {
        var dialogo = null;
        if (alvo.closest)
            dialogo = alvo.closest("[role='dialog']");
        var ordem = tabulaveis(dialogo);
        var indice = -1;
        for (var i = 0; i < ordem.length; i++) {
            if (ordem[i] === alvo) {
                indice = i;
                break;
            }
        }
        if (indice < 0)
            return;

        var popover = popoverDoMenu.get(alvo);
        var passo = direcao < 0 ? -1 : 1;
        var escolhido = seguinteNaOrdem(ordem, indice + passo, passo, popover);
        if (!escolhido && dialogo)
            escolhido = voltaNoDialogo(ordem, passo, popover);
        if (escolhido)
            escolhido.focus();
    }

    function seguinteNaOrdem(ordem, inicio, passo, popover) {
        for (var j = inicio; j >= 0 && j < ordem.length; j = j + passo) {
            if (!estaNoPopover(ordem[j], popover))
                return ordem[j];
        }
        return null;
    }

    function voltaNoDialogo(ordem, passo, popover) {
        var inicio = passo < 0 ? ordem.length - 1 : 0;
        return seguinteNaOrdem(ordem, inicio, passo, popover);
    }

    function estaNoPopover(candidato, popover) {
        if (!popover)
            return false;
        return candidato === popover || popover.contains(candidato);
    }

    function tabulaveis(raiz) {
        var base = document;
        if (raiz && raiz.querySelectorAll)
            base = raiz;
        var nos = base.querySelectorAll("a[href], button, input, select, textarea, [tabindex]");
        var naturais = [];
        var positivos = [];
        for (var i = 0; i < nos.length; i++) {
            var no = nos[i];
            if (!podeTabular(no))
                continue;
            var valor = valorTab(no);
            if (valor > 0)
                positivos.push({ no: no, valor: valor, ordem: i });
            else if (valor === 0)
                naturais.push(no);
        }
        positivos.sort(function (a, b) {
            if (a.valor !== b.valor)
                return a.valor - b.valor;
            return a.ordem - b.ordem;
        });
        var lista = [];
        for (var p = 0; p < positivos.length; p++)
            lista.push(positivos[p].no);
        for (var n = 0; n < naturais.length; n++)
            lista.push(naturais[n]);
        return lista;
    }

    function valorTab(no) {
        var indice = no.getAttribute("tabindex");
        if (indice === null || indice === "")
            return 0;
        var valor = parseInt(indice, 10);
        if (isNaN(valor))
            return 0;
        return valor;
    }

    function podeTabular(no) {
        if (!no || no.disabled)
            return false;
        if (no.tagName === "INPUT" && no.type === "hidden")
            return false;
        if (no.getAttribute("tabindex") === "-1")
            return false;
        if (no.hasAttribute("hidden"))
            return false;
        if (no.classList && no.classList.contains("fixed") && no.classList.contains("pointer-events-none"))
            return false;
        var estilo = window.getComputedStyle(no);
        if (!estilo || estilo.display === "none" || estilo.visibility === "hidden")
            return false;
        if (estilo.position === "fixed" && estilo.pointerEvents === "none")
            return false;
        if (no.getClientRects().length === 0)
            return false;
        if (no.offsetWidth === 0 || no.offsetHeight === 0)
            return false;
        return true;
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

    document.addEventListener("keydown", aoTeclar, true);
    observador.observe(document.documentElement, {
        attributes: true,
        attributeFilter: ["aria-expanded"],
        childList: true,
        subtree: true
    });
    varrer(document.querySelectorAll(".mud-menu-activator"));
})();
