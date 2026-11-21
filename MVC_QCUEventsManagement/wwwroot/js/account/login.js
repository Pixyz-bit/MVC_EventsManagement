const password = document.getElementById("password");
const visibilityButton = document.getElementById("password-visibility");

visibilityButton.addEventListener("click", () => {
    const reveal = password.type === "password";
    password.type = reveal ? "text" : "password";
    visibilityButton.textContent = reveal ? "Hide" : "Show";
    visibilityButton.setAttribute("aria-label", reveal ? "Hide password" : "Show password");
});

visibilityButton.hidden = false;
