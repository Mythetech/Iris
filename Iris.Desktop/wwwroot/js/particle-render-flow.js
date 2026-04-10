const RENDERER = {
    BASE_PARTICLE_COUNT: 30,
    WATCH_INTERVAL: 100,

    init: function() {
        this.setParameters();
        this.reconstructMethods();
        this.setup();
        this.bindEvent();
        this.render();
    },

    setParameters: function() {
        this.windowElement = window;
        this.containerElement = document.getElementById('particle-canvas');

        this.canvas = document.createElement('canvas');
        this.context = this.canvas.getContext('2d');
        this.containerElement.appendChild(this.canvas);

        this.particles = [];
        this.watchIds = [];

        this.gravity = {
            x: 0,
            y: 0,
            on: false,
            radius: 100,
            gravity: true
        };

        this.hueOffset = 0;
    },

    setup: function() {
        this.particles.length = 0;
        this.watchIds.length = 0;

        var rect = this.containerElement.getBoundingClientRect();
        this.width = rect.width;
        this.height = rect.height;

        this.canvas.width = this.width;
        this.canvas.height = this.height;

        this.distance = Math.sqrt(
            Math.pow(this.width / 2, 2) + Math.pow(this.height / 2, 2)
        );

        this.createParticles();
    },

    reconstructMethods: function() {
        this.watchWindowSize = this.watchWindowSize.bind(this);
        this.jdugeToStopResize = this.jdugeToStopResize.bind(this);
        this.render = this.render.bind(this);
    },

    createParticles: function() {
        var count = (this.BASE_PARTICLE_COUNT
            * this.width / 500
            * this.height / 500) | 0;

        for (var i = 0; i < count; i++) {
            this.particles.push(new PARTICLE(this));
        }
    },

    watchWindowSize: function() {
        this.clearTimer();
        this.tmpWidth = this.windowElement.innerWidth;
        this.tmpHeight = this.windowElement.innerHeight;
        this.watchIds.push(setTimeout(this.jdugeToStopResize, this.WATCH_INTERVAL));
    },

    clearTimer: function() {
        while (this.watchIds.length > 0) {
            clearTimeout(this.watchIds.pop());
        }
    },

    jdugeToStopResize: function() {
        var width = this.windowElement.innerWidth;
        var height = this.windowElement.innerHeight;
        var stopped = (width === this.tmpWidth && height === this.tmpHeight);

        this.tmpWidth = width;
        this.tmpHeight = height;

        if (stopped) {
            this.setup();
        }
    },

    bindEvent: function() {
        this.windowElement.addEventListener('resize', this.watchWindowSize);

        this.containerElement.addEventListener('mousemove',
            (e) => this.controlForce(true, e)
        );
        this.containerElement.addEventListener('mouseleave',
            (e) => this.controlForce(false, e)
        );
    },

    controlForce: function(on, event) {
        this.gravity.on = on;
        if (!on) return;

        var offset = this.containerElement.getBoundingClientRect();
        this.gravity.x = event.clientX - offset.left;
        this.gravity.y = event.clientY - offset.top;
    },

    render: function() {
        requestAnimationFrame(this.render);

        this.hueOffset += 0.3;

        var context = this.context;
        context.save();
        context.fillStyle = 'rgba(0, 0, 0, 0.3)';
        context.fillRect(0, 0, this.width, this.height);

        context.globalCompositeOperation = 'lighter';

        var particles = this.particles;
        var gravity = this.gravity;
        var count = particles.length;

        for (var i = 0; i < count; i++) {
            var particle = particles[i];

            for (var j = i + 1; j < count; j++) {
                particle.checkForce(context, particles[j]);
            }
            particle.checkForce(context, gravity);
            particle.setHueOffset(this.hueOffset);
            particle.render(context);
        }

        context.restore();
    }
};

var PARTICLE = function(renderer) {
    this.renderer = renderer;
    this.init();
};

PARTICLE.prototype = {
    THRESHOLD: 100,
    SPRING_AMOUNT: 0.001,
    LIMIT_RATE: 0.2,
    GRAVIY_MAGINIFICATION: 10,

    init: function() {
        this.radius = this.getRandomValue(3, 8);

        this.x = this.getRandomValue(
            -this.renderer.width * this.LIMIT_RATE,
            this.renderer.width * (1 + this.LIMIT_RATE)
        ) | 0;
        this.y = this.getRandomValue(
            -this.renderer.height * this.LIMIT_RATE,
            this.renderer.height * (1 + this.LIMIT_RATE)
        ) | 0;

        this.vx = this.getRandomValue(-3, 3);
        this.vy = this.getRandomValue(-3, 3);
        this.ax = 0;
        this.ay = 0;
        this.gravity = false;

        this.transformShape();
    },

    getRandomValue: function(min, max) {
        return min + (max - min) * Math.random();
    },

    transformShape: function() {
        var velocity = Math.sqrt(this.vx * this.vx + this.vy * this.vy);

        this.scale = 1 - velocity / 15;
        this.scaleY = 1 + velocity / 10;

        this.hue = ( (this.hueOffset + 180) + velocity * 12 ) % 360;
    },

    setHueOffset: function(offset) {
        this.hueOffset = offset;
    },

    checkForce: function(context, particle) {
        if (particle.gravity && !particle.on) {
            return;
        }

        var dx = particle.x - this.x;
        var dy = particle.y - this.y;
        var distance = Math.sqrt(dx * dx + dy * dy);

        var magnification = (particle.gravity ? this.GRAVIY_MAGINIFICATION : 1);

        if (distance > this.THRESHOLD * magnification) {
            return;
        }

        var rate = this.SPRING_AMOUNT / magnification / (this.radius + particle.radius || this.radius);
        this.ax = dx * rate * (particle.radius || 0);
        this.ay = dy * rate * (particle.radius || 0);

        if (!particle.gravity) {
            particle.ax = -dx * rate * this.radius;
            particle.ay = -dy * rate * this.radius;
        }

        if (distance <= this.THRESHOLD * (particle.gravity ? 2 : 1)) {
            context.lineWidth = particle.gravity ? 0.5 : 2.5;
            context.strokeStyle = 'hsla(' + this.hue + ', 70%, 30%, ' +
                (Math.abs(this.THRESHOLD - distance) / this.THRESHOLD) + ')';

            context.beginPath();
            context.moveTo(this.x, this.y);
            context.lineTo(particle.x, particle.y);
            context.stroke();
        }
    },

    render: function(context) {
        context.save();
        context.fillStyle = 'hsl(' + this.hue + ', 70%, 40%)';

        context.translate(this.x, this.y);
        context.rotate(Math.atan2(this.vy, this.vx) + Math.PI / 2);
        context.scale(this.scale, this.scaleY);

        context.beginPath();
        context.arc(0, 0, this.radius, 0, Math.PI * 2, false);
        context.fill();
        context.restore();

        this.x += this.vx;
        this.y += this.vy;
        this.vx += this.ax;
        this.vy += this.ay;

        if (
            (this.x < -this.radius && this.vx < 0) ||
            (this.x > this.renderer.width + this.radius && this.vx > 0) ||
            (this.y < -this.radius && this.vy < 0) ||
            (this.y > this.renderer.height + this.radius && this.vy > 0)
        ) {
            var theta = this.getRandomValue(0, Math.PI * 2);
            var sin = Math.sin(theta);
            var cos = Math.cos(theta);
            var velocity = this.getRandomValue(-3, 3);

            this.x = -(this.renderer.distance + this.radius) * cos + this.renderer.width / 2;
            this.y = -(this.renderer.distance + this.radius) * sin + this.renderer.height / 2;
            this.vx = velocity * cos;
            this.vy = velocity * sin;
        }
        this.transformShape();
    }
};

document.addEventListener('DOMContentLoaded', function() {
    RENDERER.init();
});
