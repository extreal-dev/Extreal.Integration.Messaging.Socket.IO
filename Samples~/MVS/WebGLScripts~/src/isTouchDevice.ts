let result = true;

(() => {
    const over = 2;
    const period = 300;
    let timer: NodeJS.Timeout | null = null;
    let count = 0;

    const mousemove = () => {
        if (!timer) {
            timer = setTimeout(() => {
                timer = null;
                count = 0;
            }, period);
            return;
        }
        if (++count < over) {
            return;
        }
        document.removeEventListener("mousemove", mousemove);
        result = false;
    };

    document.addEventListener("mousemove", mousemove);
})();

const isTouchDevice = () => result;

export { isTouchDevice };
