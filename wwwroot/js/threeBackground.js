window.initThreeBackground = (canvasId) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    const scene = new THREE.Scene();
    // Deep blue background
    scene.background = new THREE.Color(0x0A0F1D); 

    const camera = new THREE.PerspectiveCamera(75, window.innerWidth / window.innerHeight, 0.1, 1000);
    const renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true });
    
    renderer.setSize(window.innerWidth, window.innerHeight);
    renderer.setPixelRatio(window.devicePixelRatio);

    // Create the XMB-style wireframe wave
    const geometry = new THREE.PlaneGeometry(20, 10, 40, 20);
    const material = new THREE.MeshBasicMaterial({ 
        color: 0xFFD700, // Gold
        wireframe: true,
        transparent: true,
        opacity: 0.3
    });

    const plane = new THREE.Mesh(geometry, material);
    plane.rotation.x = -Math.PI / 2; // Lay flat
    plane.position.y = -2;
    scene.add(plane);

    camera.position.z = 5;
    camera.position.y = 1;

    let time = 0;

    function animate() {
        requestAnimationFrame(animate);
        time += 0.01;

        // Animate vertices to create a wave effect
        const positions = geometry.attributes.position;
        for (let i = 0; i < positions.count; i++) {
            const x = positions.getX(i);
            const y = positions.getY(i);
            // Sine wave math for undulating effect
            const z = Math.sin(x * 0.5 + time) * 0.5 + Math.cos(y * 0.5 + time) * 0.5;
            positions.setZ(i, z);
        }
        geometry.attributes.position.needsUpdate = true;

        renderer.render(scene, camera);
    }

    animate();

    // Handle resize
    window.addEventListener('resize', () => {
        camera.aspect = window.innerWidth / window.innerHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(window.innerWidth, window.innerHeight);
    });
};