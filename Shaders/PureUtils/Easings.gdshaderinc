
float easeInSine(float x) {
    return 1. - cos((x * PI) / 2.);
}

float easeInSine(vec2 uv) {
    return uv.y - easeInSine(uv.x);
}

float easeOutSine(float x) {
    return sin((x * PI) / 2.);
}

float easeOutSine(vec2 uv) {
    return uv.y - easeOutSine(uv.x);
}

float easeInOutSine(float x) {
    return -(cos(PI * x) - 1.) / 2.;
}

float easeInOutSine(vec2 uv) {
    return uv.y - easeInOutSine(uv.x);
}

float easeInCubic(float x) {
    return x * x * x;
}

float easeInCubic(vec2 uv) {
    return uv.y - easeInCubic(uv.x);
}

float easeOutCubic(float x) {
    return 1. - pow(1. - x, 3.);
}

float easeOutCubic(vec2 uv) {
    return uv.y - easeOutCubic(uv.x);
}

float easeInOutCubic(float x) {
    return x < .5 ? 4. * x * x * x : 1. - pow(-2. * x + 2., 3.) / 2.;
}

float easeInOutCubic(vec2 uv) {
    return uv.y - easeInOutCubic(uv.x);
}

float easeInQuint(float x) {
    return x * x * x * x * x;
}

float easeInQuint(vec2 uv) {
    return uv.y - easeInQuint(uv.x);
}

float easeOutQuint(float x) {
    return 1. - pow(1. - x, 5.);
}

float easeOutQuint(vec2 uv) {
    return uv.y - easeOutQuint(uv.x);
}

float easeInOutQuint(float x) {
    return x < .5 ? 16. * x * x * x * x * x : 1. - pow(-2. * x + 2., 5.) / 2.;
}

float easeInOutQuint(vec2 uv) {
    return uv.y - easeInOutQuint(uv.x);
}

float easeInCirc(float x) {
    return 1. - sqrt(abs(1. - pow(x, 2.)));
}

float easeInCirc(vec2 uv) {
    return uv.y - easeInCirc(uv.x);
}

float easeOutCirc(float x) {
    return sqrt(abs(1. - pow(x - 1., 2.)));
}

float easeOutCirc(vec2 uv) {
    return uv.y - easeOutCirc(uv.x);
}

float easeInOutCirc(float x) {
    return x < .5
      ? (1. - sqrt(1. - pow(2. * x, 2.))) / 2.
      : (sqrt(1. - pow(-2. * x + 2., 2.)) + 1.) / 2.;
}

float easeInOutCirc(vec2 uv) {
    return uv.y - easeInOutCirc(uv.x);
}

float easeInElastic(float x) {
    float c4 = (2. * PI) / 3.;

    return x == 0.
      ? 0.
      : x == 1.
      ? 1.
      : -pow(2., 10. * x - 10.) * sin((x * 10. - 10.75) * c4);
}

float easeInElastic(vec2 uv) {
    return uv.y - easeInElastic(uv.x);
}

float easeOutElastic(float x) {
    float c4 = (2. * PI) / 3.;

    return x == 0.
      ? 0.
      : x == 1.
      ? 1.
      : pow(2., -10. * x) * sin((x * 10. - .75) * c4) + 1.;
}

float easeOutElastic(vec2 uv) {
    return uv.y - easeOutElastic(uv.x);
}

float easeInOutElastic(float x) {
    float c5 = (2. * PI) / 4.5;

    return x == 0.
      ? 0.
      : x == 1.
      ? 1.
      : x < .5
      ? -(pow(2., 20. * x - 10.) * sin((20. * x - 11.125) * c5)) / 2.
      : (pow(2., -20. * x + 10.) * sin((20. * x - 11.125) * c5)) / 2. + 1.;
}

float easeInOutElastic(vec2 uv) {
    return uv.y - easeInOutElastic(uv.x);
}

float easeInQuad(float x) {
    return x * x;
}

float easeInQuad(vec2 uv) {
    return uv.y - easeInQuad(uv.x);
}

float easeOutQuad(float x) {
    return 1. - (1. - x) * (1. - x);
}

float easeOutQuad(vec2 uv) {
    return uv.y - easeOutQuad(uv.x);
}

float easeInOutQuad(float x) {
    return x < .5 ? 2. * x * x : 1. - pow(-2. * x + 2., 2.) / 2.;
}

float easeInOutQuad(vec2 uv) {
    return uv.y - easeInOutQuad(uv.x);
}

float easeInQuart(float x) {
    return x * x * x * x;
}

float easeInQuart(vec2 uv) {
    return uv.y - easeInQuart(uv.x);
}

float easeOutQuart(float x) {
    return 1. - pow(1. - x, 4.);
}

float easeOutQuart(vec2 uv) {
    return uv.y - easeOutQuart(uv.x);
}

float easeInOutQuart(float x) {
    return x < .5 ? 8. * x * x * x * x : 1. - pow(-2. * x + 2., 4.) / 2.;
}

float easeInOutQuart(vec2 uv) {
    return uv.y - easeInOutQuart(uv.x);
}

float easeInExpo(float x) {
    return x == 0. ? 0. : pow(2., 10. * x - 10.);
}

float easeInExpo(vec2 uv) {
    return uv.y - easeInExpo(uv.x);
}

float easeOutExpo(float x) {
    return x == 1. ? 1. : 1. - pow(2., -10. * x);
}

float easeOutExpo(vec2 uv) {
    return uv.y - easeOutExpo(uv.x);
}

float easeInOutExpo(float x) {
    return x == 0.
      ? 0.
      : x == 1.
      ? 1.
      : x < .5 ? pow(2., 20. * x - 10.) / 2.
      : (2. - pow(2., -20. * x + 10.)) / 2.;
}


float easeInOutExpo(vec2 uv) {
    return uv.y - easeInOutExpo(uv.x);
}

float easeInBack(float x) {
    float c1 = 1.70158;
    float c3 = c1 + 1.;

    return c3 * x * x * x - c1 * x * x;
}

float easeInBack(vec2 uv) {
    return uv.y - easeInBack(uv.x);
}

float easeOutBack(float x) {
    float c1 = 1.70158;
    float c3 = c1 + 1.;

    return 1. + c3 * pow(x - 1., 3.) + c1 * pow(x - 1., 2.);
}

float easeOutBack(vec2 uv) {
    return uv.y - easeOutBack(uv.x);
}

float easeInOutBack(float x) {
    float c1 = 1.70158;
    float c2 = c1 * 1.525;

    return x < .5
      ? (pow(2. * x, 2.) * ((c2 + 1.) * 2. * x - c2)) / 2.
      : (pow(2. * x - 2., 2.) * ((c2 + 1.) * (x * 2. - 2.) + c2) + 2.) / 2.;
}

float easeInOutBack(vec2 uv) {
    return uv.y - easeInOutBack(uv.x);
}

float easeOutBounce(float x) {
    float n1 = 7.5625;
    float d1 = 2.75;

    if (x < 1. / d1) {
        return n1 * x * x;
    } else if (x < 2. / d1) {
        return n1 * (x -= 1.5 / d1) * x + 0.75;
    } else if (x < 2.5 / d1) {
        return n1 * (x -= 2.25 / d1) * x + 0.9375;
    } else {
        return n1 * (x -= 2.625 / d1) * x + 0.984375;
    }
}

float easeOutBounce(vec2 uv) {
    return uv.y - easeOutBounce(uv.x);
}

float easeInBounce(float x) {
    return 1. - easeOutBounce(1. - x);
}

float easeInBounce(vec2 uv) {
    return uv.y - easeInBounce(uv.x);
}

float easeInOutBounce(float x) {
    return x < .5
      ? (1. - easeOutBounce(1. - 2. * x)) / 2.
      : (1. + easeOutBounce(2. * x - 1.)) / 2.;
}

float easeInOutBounce(vec2 uv) {
    return uv.y - easeInOutBounce(uv.x);
}