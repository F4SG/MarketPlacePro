document.addEventListener("DOMContentLoaded", () => {
    const rol = document.getElementById("Rol");
    const tienda = document.getElementById("NombreTienda");
    if (rol && tienda) {
        const actualizarTienda = () => {
            tienda.required = rol.value === "vendedor";
            tienda.setAttribute("aria-required", String(tienda.required));
        };
        rol.addEventListener("change", actualizarTienda);
        actualizarTienda();
    }
});
