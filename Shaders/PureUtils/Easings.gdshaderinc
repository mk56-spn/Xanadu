

float linear(float t) {
    return t;
}

float inQuad(float t) {
    return t * t;
}

float outQuad(float t) {
    return 1.0 - inQuad(1.0 - t);
}

float inOutQuad(float t) {
    return t < 0.5 ? inQuad(t * 2.0) / 2.0 : 1.0 - inQuad((1.0 - t) * 2.0) / 2.0;
}

float inCubic(float t) {
    return t * t * t;
}

float outCubic(float t) {
    return 1.0 - inCubic(1.0 - t);
}

float inOutCubic(float t) {
    return t < 0.5 ? inCubic(t * 2.0) / 2.0 : 1.0 - inCubic((1.0 - t) * 2.0) / 2.0;
}

float inQuart(float t) {
    return t * t * t * t;
}

float outQuart(float t) {
    return 1.0 - inQuart(1.0 - t);
}

float inOutQuart(float t) {
    return t < 0.5 ? inQuart(t * 2.0) / 2.0 : 1.0 - inQuart((1.0 - t) * 2.0) / 2.0;
}

float inQuint(float t) {
    return t * t * t * t * t;
}

float outQuint(float t) {
    return 1.0 - inQuint(1.0 - t);
}

float inOutQuint(float t) {
    return t < 0.5 ? inQuint(t * 2.0) / 2.0 : 1.0 - inQuint((1.0 - t) * 2.0) / 2.0;
}

float inSine(float t) {
    return 1.0 - cos(t * PI / 2.0);
}

float outSine(float t) {
    return sin(t * PI / 2.0);
}

float inOutSine(float t) {
    return (cos(t * PI) - 1.0) / -2.0;
}

float inExpo(float t) {
    return pow(2.0, 10.0 * (t - 1.0));
}

float outExpo(float t) {
    return 1.0 - inExpo(1.0 - t);
}

float inOutExpo(float t) {
    return t < 0.5 ? inExpo(t * 2.0) / 2.0 : 1.0 - inExpo((1.0 - t) * 2.0) / 2.0;
}

float inCirc(float t) {
    return -(sqrt(1.0 - t * t) - 1.0);
}

float outCirc(float t) {
    return 1.0 - inCirc(1.0 - t);
}

float inOutCirc(float t) {
    return t < 0.5 ? inCirc(t * 2.0) / 2.0 : 1.0 - inCirc((1.0 - t) * 2.0) / 2.0;
}

float outElastic(float t) {
    return pow(2.0, -10.0 * t) * sin((t - 0.3 / 4.0) * (2.0 * PI) / 0.3) + 1.0;
}

float inElastic(float t) {
    return 1.0 - outElastic(1.0 - t);
}

float inOutElastic(float t) {
    return t < 0.5 ? inElastic(t * 2.0) / 2.0 : 1.0 - inElastic((1.0 - t) * 2.0) / 2.0;
}

float inBack(float t) {
    float s = 1.70158;
    return t * t * ((s + 1.0) * t - s);
}

float outBack(float t) {
    return 1.0 - inBack(1.0 - t);
}

float inOutBack(float t) {
    return t < 0.5 ? inBack(t * 2.0) / 2.0 : 1.0 - inBack((1.0 - t) * 2.0) / 2.0;
}

float outBounce(float t) {
    if (t < 1.0 / 2.75) {
        return 7.5625 * t * t;
    } else if (t < 2.0 / 2.75) {
        t -= 1.5 / 2.75;
        return 7.5625 * t * t + 0.75;
    } else if (t < 2.5 / 2.75) {
        t -= 2.25 / 2.75;
        return 7.5625 * t * t + 0.9375;
    } else {
        t -= 2.625 / 2.75;
        return 7.5625 * t * t + 0.984375;
    }
}

float inBounce(float t) {
    return 1.0 - outBounce(1.0 - t);
}

float inOutBounce(float t) {
    return t < 0.5 ? inBounce(t * 2.0) / 2.0 : 1.0 - inBounce((1.0 - t) * 2.0) / 2.0;
}

float getEasing(int easingType, float progress) {
    switch (easingType) {
        case 0: return linear(progress);
        case 1: return inQuad(progress);
        case 2: return outQuad(progress);
        case 3: return inOutQuad(progress);
        case 4: return inCubic(progress);
        case 5: return outCubic(progress);
        case 6: return inOutCubic(progress);
        case 7: return inQuart(progress);
        case 8: return outQuart(progress);
        case 9: return inOutQuart(progress);
        case 10: return inQuint(progress);
        case 11: return outQuint(progress);
        case 12: return inOutQuint(progress);
        case 13: return inSine(progress);
        case 14: return outSine(progress);
        case 15: return inOutSine(progress);
        case 16: return inExpo(progress);
        case 17: return outExpo(progress);
        case 18: return inOutExpo(progress);
        case 19: return inCirc(progress);
        case 20: return outCirc(progress);
        case 21: return inOutCirc(progress);
        case 22: return inElastic(progress);
        case 23: return outElastic(progress);
        case 24: return inOutElastic(progress);
        case 25: return inBack(progress);
        case 26: return outBack(progress);
        case 27: return inOutBack(progress);
        case 28: return inBounce(progress);
        case 29: return outBounce(progress);
        case 30: return inOutBounce(progress);
    }
    return progress; // Default to linear
}