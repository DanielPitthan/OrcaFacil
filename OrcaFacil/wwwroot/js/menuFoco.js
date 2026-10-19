window.OrcaFacilMenu = {
    umFoco: function (botao) {
        const pai = botao && botao.parentElement;
        if (!pai || pai.getAttribute("role") !== "button")
            return;

        pai.tabIndex = -1;
        pai.removeAttribute("role");
    }
};
