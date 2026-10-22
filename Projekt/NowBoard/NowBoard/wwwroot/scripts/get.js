export class Get {
    time() {
        // aktuelle zeit holen
        const now = new Date();
        const hours = (now.getUTCHours() + 1) % 24; // für schweizer sommerzeit utc+2, für winterzeit/normalzeit utc+1
        const minutes = now.getUTCMinutes();
        const seconds = now.getUTCSeconds();

        return { hours, minutes, seconds };
    }

    winkel() {
        const { hours, minutes, seconds } = this.time();
        // winkel berechnen, konvertieren in radian für sinus und cosinus funktionen
        const hourwinkel = (hours * Math.PI) / 6 + (minutes * Math.PI) / 360; // 30 grad pro h + 0.5 grad pro min
        const minutewinkel = (minutes * Math.PI) / 30 + (seconds * Math.PI) / 1800; // 6 grad pro min + 0.1 grad pro sek
        const secondwinkel = (seconds * Math.PI) / 30; // 6 grad pro sek
        const littlesecondwinkel = ((seconds + 30) * Math.PI) / 30;
        const littleminutewinkel = ((minutes + 30) * Math.PI) / 30 + (seconds * Math.PI) / 1800;
        const littlehourwinkel = ((hours + 6) * Math.PI) / 6 + (minutes * Math.PI) / 360;

        return { hourwinkel, minutewinkel, secondwinkel, littlesecondwinkel, littleminutewinkel, littlehourwinkel };
    }

    zeigerlängen() {
        const { radius } = this;
        // zeigerlängen holen
        const hourHandLength = radius * 0.6;
        const minuteHandLength = radius * 0.8;
        const secondHandLength = radius * 0.9;

        return { hourHandLength, minuteHandLength, secondHandLength };
    }
}