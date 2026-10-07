namespace StarHealth.Core.Localization;

/// <summary>
/// All user-visible strings, Spanish + English. Single source so WinUI and
/// future Uno heads share localization with zero platform code.
/// Missing keys return the key itself (visible in screenshots, never blank).
/// Persistence (LocalSettings) lives in the app head; Core only holds values.
/// </summary>
public static class Text
{
    private static readonly Dictionary<string, (string Es, string En)> S = new()
    {
        // Shell nav
        ["nav.status"] = ("En línea", "Online"),
        ["nav.stats"] = ("Estadísticas", "Statistics"),
        ["nav.network"] = ("Red", "Network"),
        ["nav.account"] = ("Suscripción", "Subscription"),
        ["nav.obst"] = ("Obstrucciones", "Obstructions"),
        ["nav.align"] = ("Alineación", "Alignment"),
        ["nav.speed"] = ("Prueba de velocidad", "Speed test"),
        ["nav.settings"] = ("Configuración", "Settings"),
        ["nav.help"] = ("Asistencia", "Support"),
        ["nav.about"] = ("Acerca de la app", "About the app"),
        // Status page
        ["card.ping"] = ("PING EXITOSO", "PING SUCCESS"),
        ["card.ping.sub"] = ("últimos 15 minutos", "last 15 minutes"),
        ["card.lat"] = ("LATENCIA", "LATENCY"),
        ["card.lat.sub"] = ("mediana, últimos 15 minutos", "median, last 15 minutes"),
        ["card.throughput"] = ("DESCARGA · SUBIDA", "DOWNLOAD · UPLOAD"),
        ["card.obst"] = ("OBSTRUCCIÓN", "OBSTRUCTION"),
        ["card.align"] = ("ALINEACIÓN", "ALIGNMENT"),
        ["cta.align"] = ("Alinee su equipo Starlink", "Align your Starlink"),
        // Stats page
        ["stats.title"] = ("Estadísticas", "Statistics"),
        ["perf.title"] = ("RENDIMIENTO · VENTANA CONTINUA", "THROUGHPUT · ROLLING WINDOW"),
        ["perf.note"] = ("Uso real por segundo. La antena solo guarda ~15 minutos; esta app acumula la ventana mientras está abierta.", "Actual per-second usage. The dish only keeps ~15 minutes; this app accumulates the window while open."),
        ["usage.title"] = ("USO TOTAL", "TOTAL USAGE"),
        ["usage.sub"] = ("Contadores de la antena", "Dish counters"),
        ["signal.title"] = ("SEÑAL Y PÉRDIDA", "SIGNAL & LOSS"),
        ["signal.snr.note"] = ("El firmware actual ya no informa SNR; «—» significa no disponible, no mala señal.", "Current firmware no longer reports SNR; “—” means unavailable, not bad signal."),
        ["power.title"] = ("ENERGÍA", "POWER"),
        ["outages.title"] = ("INTERRUPCIONES", "OUTAGES"),
        ["events.title"] = ("EVENTOS E INTERRUPCIONES", "EVENTS & OUTAGES"),
        ["events.brief"] = ("Eventos breves (< 1 s)", "Brief events (< 1 s)"),
        ["device.title"] = ("EQUIPO", "DISH"),
        ["device.hw"] = ("Hardware", "Hardware"),
        ["device.sw"] = ("Software", "Software"),
        ["device.update"] = ("Actualización", "Update"),
        ["device.id"] = ("ID / Actividad", "ID / Uptime"),
        ["device.uptime"] = ("Actividad", "Uptime"),
        ["device.reboot"] = ("Motivo del último reinicio", "Last reboot reason"),
        ["device.batt"] = ("Batería", "Battery"),
        ["device.loc"] = ("Ubicación", "Location"),
        ["alerts.title"] = ("ALERTAS", "ALERTS"),
        ["alerts.none"] = ("Sin alertas activas", "No active alerts"),
        // Obstructions page
        ["obst.title"] = ("Obstrucciones", "Obstructions"),
        ["obst.total"] = ("CIELO OBSTRUIDO · 24 H", "BLOCKED SKY · 24 H"),
        ["obst.wedges"] = ("POR DIRECCIÓN · 12 CUÑAS DE 30°", "BY DIRECTION · 12 WEDGES OF 30°"),
        ["obst.wedges.note"] = ("El firmware actual ya no detalla la obstrucción por cuña; la fracción total de arriba sí sigue disponible.", "Current firmware no longer reports per-wedge obstruction; the total fraction above is still available."),
        ["obst.map"] = ("MAPA DEL CIELO", "SKY MAP"),
        ["obst.map.update"] = ("Actualizar", "Refresh"),
        ["obst.map.caption"] = ("Vista de la bóveda celeste como en la app oficial: el anillo exterior es el horizonte. Versión preliminar.", "Dome view of the sky as in the official app: the outer ring is the horizon. Early draft."),
        ["obst.map.soon"] = ("Próximamente", "Coming soon"),
        ["obst.map.loading"] = ("Cargando mapa…", "Loading map…"),
        ["obst.map.fail"] = ("La antena no devolvió mapa.", "The dish returned no map."),
        ["obst.map.empty"] = ("Sin cargar", "Not loaded"),
        // Alignment page
        ["align.title"] = ("Alineación", "Alignment"),
        ["align.notice.sub"] = ("Siga los pasos de abajo y el aviso desaparecerá cuando la antena lo confirme.", "Follow the steps below and the warning will clear once the dish confirms it."),
        ["align.current"] = ("ORIENTACIÓN ACTUAL", "CURRENT ORIENTATION"),
        ["align.guide"] = ("CÓMO ALINEAR EL MINI", "HOW TO ALIGN THE MINI"),
        ["align.g1"] = ("1. Coloque el Mini con el cielo lo más despejado posible, lejos de árboles y tejados.", "1. Place the Mini with as clear a sky view as possible, away from trees and roofs."),
        ["align.g2"] = ("2. Nivele la base: el aviso de «mástil no vertical» aparece cuando la antena detecta inclinación.", "2. Level the base: the “mast not vertical” warning appears when the dish detects tilt."),
        ["align.g3"] = ("3. Gire el Mini en pasos pequeños y espere 1–2 minutos entre ajustes; la obstrucción tarda en recalcularse.", "3. Rotate the Mini in small steps and wait 1–2 minutes between adjustments; obstruction takes time to recompute."),
        ["align.g4"] = ("4. Compruebe el resultado en Obstrucciones: busque menos del 2 % para un servicio estable.", "4. Check the result under Obstructions: aim for under 2% for stable service."),
        ["align.gnote"] = ("La app oficial muestra el objetivo exacto en grados porque lo calcula su nube con su ubicación de servicio; esta app muestra lo que la antena informa en local. El procedimiento físico es el mismo.", "The official app shows the exact target in degrees because its cloud computes it from your service location; this app shows what the dish reports locally. The physical procedure is the same."),
        // Network page
        ["net.title"] = ("Red", "Network"),
        ["net.dish"] = ("ANTENA", "DISH"),
        ["net.router"] = ("ROUTER", "ROUTER"),
        ["net.router.name"] = ("Router Starlink · 192.168.1.1:9000 (gRPC)", "Starlink router · 192.168.1.1:9000 (gRPC)"),
        ["net.router.check"] = ("Comprobar router", "Check router"),
        ["net.router.pending"] = ("Sin comprobar todavía.", "Not checked yet."),
        ["net.router.checking"] = ("Contactando 192.168.1.1:9000…", "Contacting 192.168.1.1:9000…"),
        ["net.router.ok"] = ("Router alcanzado. Habla gRPC como la antena: el siguiente paso es leer sus clientes Wi-Fi.", "Router reached. It speaks gRPC like the dish: next step is reading its Wi-Fi clients."),
        ["net.router.fail"] = ("Sin conexión al router. Normal si usa otro router o el Starlink está en modo bypass: en ese caso no hay API de router que leer.", "No router connection. Normal with a third-party router or Starlink in bypass mode: then there is no router API to read."),
        ["net.router.note"] = ("La lista de dispositivos Wi-Fi, la intensidad por cliente y la prueba del lado router salen de la API local del router, no de la antena. La lista de clientes llega en el próximo corte; la conectividad de arriba ya funciona.", "The Wi-Fi device list, per-client signal and router-side test come from the router's local API, not the dish. The client list lands in the next slice; reachability above already works."),
        ["net.eth"] = ("Puerto Ethernet de la antena", "Dish Ethernet port"),
        ["net.eth.none"] = ("sin enlace Ethernet", "no Ethernet link"),
        // Account page
        ["acct.title"] = ("Suscripción", "Subscription"),
        ["acct.plan"] = ("TU PLAN (LOCAL)", "YOUR PLAN (LOCAL)"),
        ["acct.plan.note"] = ("La antena no sabe tu plan; la app oficial lo trae de tu cuenta SpaceX. Guárdalo aquí y aparecerá en la cabecera.", "The dish doesn't know your plan; the official app fetches it from your SpaceX account. Save it here and it shows in the header."),
        ["acct.plan.header"] = ("Plan", "Plan"),
        ["acct.only"] = ("SOLO EN LA APP OFICIAL", "OFFICIAL APP ONLY"),
        ["acct.b1"] = ("· Detalle del plan, facturación y datos de la cuenta", "· Plan details, billing and account data"),
        ["acct.b2"] = ("· Recomendaciones y promociones ($40 por referido)", "· Referrals and promos ($40 per referral)"),
        ["acct.b3"] = ("· Chat de asistencia y dirección de servicio", "· Support chat and service address"),
        ["acct.note"] = ("Todo esto vive en la nube privada de SpaceX y exige iniciar sesión; no hay API pública ni sale de la antena. No se puede replicar sin credenciales, y cualquier integración no oficial se rompería a cada cambio. Esta app no te pedirá tu contraseña.", "All of this lives in SpaceX's private cloud and requires login; there is no public API and it doesn't come from the dish. It can't be replicated without credentials, and any unofficial integration would break on every change. This app will never ask for your password."),
        // Speed page
        ["spd.title"] = ("Prueba de velocidad", "Speed test"),
        ["spd.dl"] = ("DESCARGA · ESTE EQUIPO", "DOWNLOAD · THIS DEVICE"),
        ["spd.start"] = ("Iniciar prueba", "Start test"),
        ["spd.running"] = ("Midiendo…", "Measuring…"),
        ["spd.none"] = ("Sin mediciones todavía", "No measurements yet"),
        ["spd.note"] = ("Descarga 25 MB desde la red de Cloudflare y mide el tiempo real. Es la velocidad que ve este PC, no la prueba interna antena→internet de la app oficial (esa la orquesta su nube y no está expuesta en local).", "Downloads 25 MB from Cloudflare's network and measures real time. That's the speed this PC sees, not the official app's internal dish→internet test (orchestrated by their cloud, not exposed locally)."),
        ["spd.cont"] = ("LECTURA CONTINUA (SIN PRUEBA)", "LIVE READING (NO TEST)"),
        ["spd.cont.note"] = ("Tráfico real que la antena informa ahora mismo. Para ver picos, genere tráfico (un vídeo 4K, una descarga) y mire Estadísticas.", "Real traffic the dish reports right now. To see peaks, generate traffic (a 4K video, a download) and watch Statistics."),
        // Settings page
        ["cfg.title"] = ("Configuración", "Settings"),
        ["cfg.conn"] = ("CONEXIÓN", "CONNECTION"),
        ["cfg.conn.note"] = ("Sondeo cada 2 s por gRPC sin autenticar (HTTP/2 en claro). Si usa su propio router, necesita ruta estática a 192.168.100.1; si la app oficial funciona en su red, esta también.", "Polls every 2 s over unauthenticated gRPC (cleartext HTTP/2). With your own router you need a static route to 192.168.100.1; if the official app works on your network, so does this one."),
        ["cfg.src"] = ("DE DÓNDE SALE CADA DATO", "WHERE EACH DATUM COMES FROM"),
        ["cfg.src.dish"] = ("Estado, actividad, throughput, latencia, pérdida, obstrucción total, alertas, equipo, software, ubicación (si la autoriza), orientación, energía (si el hardware la informa), estadísticas de 15 min.", "Status, uptime, throughput, latency, loss, total obstruction, alerts, hardware, software, location (if authorized), orientation, power (if hardware reports it), 15-min stats."),
        ["cfg.src.router"] = ("Dispositivos Wi-Fi, señal por cliente, pruebas del lado router. Otra API gRPC (192.168.1.1:9000); no sale de la antena.", "Wi-Fi devices, per-client signal, router-side tests. A separate gRPC API (192.168.1.1:9000); doesn't come from the dish."),
        ["cfg.src.acct"] = ("Plan, facturación, referidos, chat de soporte, objetivo exacto de alineación. Nube privada con sesión; sin API pública.", "Plan, billing, referrals, support chat, exact alignment target. Private cloud with login; no public API."),
        ["cfg.src.der"] = ("Ping exitoso y latencia mediana (calculados del historial), interrupciones con hora (lista de la antena), prueba de velocidad (medición de este PC), plan (lo escribe usted).", "Ping success and median latency (computed from history), timestamped outages (dish list), speed test (this PC's measurement), plan (you type it)."),
        ["cfg.src.intro"] = ("La app oficial mezcla tres fuentes. Esta app lee la primera, parte de la segunda y marca el resto con honestidad.", "The official app mixes three sources. This app reads the first, parts of the second, and honestly marks the rest."),
        ["src.dish"] = ("ANTENA · LOCAL", "DISH · LOCAL"),
        ["src.router"] = ("ROUTER · LOCAL", "ROUTER · LOCAL"),
        ["src.acct"] = ("CUENTA SPACEX", "SPACEX ACCOUNT"),
        ["src.der"] = ("DERIVADO", "DERIVED"),
        ["src.dev"] = ("ESTE EQUIPO", "THIS DEVICE"),
        ["src.local"] = ("MEDICIÓN LOCAL", "LOCAL MEASUREMENT"),
        ["cfg.fw"] = ("LO QUE YA NO DA EL FIRMWARE", "WHAT CURRENT FIRMWARE NO LONGER PROVIDES"),
        ["cfg.fw.1"] = ("· SNR por estado y por muestra: obsoleto, llega a 0. «—» significa no informado.", "· Per-status and per-sample SNR: obsolete, reads 0. “—” means unreported."),
        ["cfg.fw.2"] = ("· Obstrucción por cuña (12 valores): obsoleto en firmware reciente; la fracción total sigue viva.", "· Per-wedge obstruction (12 values): obsolete on recent firmware; the total fraction still works."),
        ["cfg.fw.3"] = ("· Energía: 0,0 en hardware que no la soporta. Este Mini sí la informa por historial.", "· Power: 0.0 on hardware without support. This Mini does report it via history."),
        ["cfg.fw.4"] = ("· Ubicación: exige autorizarla en la app oficial (Ajustes → Avanzado → Datos de depuración).", "· Location: must be authorized in the official app (Settings → Advanced → Debug data)."),
        ["cfg.alt"] = ("ALTERNATIVAS CUANDO LA ANTENA NO LO DA", "ALTERNATIVES WHEN THE DISH DOESN'T PROVIDE IT"),
        ["cfg.alt.1"] = ("· Energía real: enchufe inteligente con medición (Shelly/Tapo, API local) o SAI con USB.", "· Real power: smart plug with metering (Shelly/Tapo, local API) or UPS over USB."),
        ["cfg.alt.2"] = ("· Historial largo: esta app acumula mientras está abierta; para meses, guarde el sondeo en SQLite/InfluxDB (como starlink-grpc-tools).", "· Long history: this app accumulates while open; for months, persist polling to SQLite/InfluxDB (like starlink-grpc-tools)."),
        ["cfg.alt.3"] = ("· Datos de cuenta: solo sesión oficial. La vía no oficial es interceptar su propia sesión (mitmproxy) y reutilizar el token: frágil y fuera del alcance de esta app.", "· Account data: official session only. The unofficial route is intercepting your own session (mitmproxy) and reusing the token: fragile and out of scope here."),
        ["cfg.alt.4"] = ("· Acceso remoto: solo vía nube SpaceX (app oficial). Esta app es de red local por diseño.", "· Remote access: only via SpaceX cloud (official app). This app is LAN-only by design."),
        ["cfg.eq"] = ("EQUIPO (LEÍDO DE LA ANTENA)", "DISH (READ FROM HARDWARE)"),
        ["cfg.eq.snow"] = ("Derretir nieve", "Snow melt"),
        ["cfg.eq.psave"] = ("Ahorro de energía", "Power save"),
        ["cfg.eq.loc"] = ("Ubicación solicitada", "Location requested"),
        ["cfg.eq.on"] = ("Activado", "Enabled"),
        ["cfg.eq.off"] = ("Desactivado", "Disabled"),
        ["cfg.lang"] = ("IDIOMA", "LANGUAGE"),
        // Help page
        ["help.title"] = ("Asistencia", "Support"),
        ["help.official"] = ("AYUDA OFICIAL", "OFFICIAL HELP"),
        ["help.chat"] = ("El chat con soporte y las gestiones de cuenta solo existen en la app oficial con sesión iniciada.", "Support chat and account management only exist in the official app when signed in."),
        ["help.about"] = ("ACERCA DE ESTA APP", "ABOUT THIS APP"),
        ["help.body"] = ("StarHealth · monitor local de antena Starlink. Lee la API gRPC local sin autenticar; nunca pide tus credenciales de SpaceX.", "StarHealth · local Starlink dish monitor. Reads the local unauthenticated gRPC API; never asks for your SpaceX credentials."),
        // About page
        ["about.title"] = ("Acerca de la app", "About the app"),
        ["about.tagline"] = ("Monitor de escritorio para tu Starlink Mini: estado, velocidad, obstrucciones y alineación, leídos directamente de la antena en tu red local.", "Desktop monitor for your Starlink Mini: status, speed, obstructions and alignment, read straight from the dish on your local network."),
        ["about.how"] = ("CÓMO FUNCIONA", "HOW IT WORKS"),
        ["about.how.body"] = ("La antena expone un API gRPC sin autenticar en 192.168.100.1:9200. Esta app descubre su esquema por reflexión (sin .proto fijos que se queden obsoletos), sondea estado e historial cada 2 segundos y calcula el resto en local: ping exitoso, latencia mediana, interrupciones y mapa del cielo.", "The dish exposes an unauthenticated gRPC API at 192.168.100.1:9200. This app discovers its schema via reflection (no fixed .proto files to go stale), polls status and history every 2 seconds, and derives the rest locally: ping success, median latency, outages and the sky map."),
        ["about.stack"] = ("TECNOLOGÍA", "STACK"),
        ["about.stack.body"] = ("WinUI 3 nativo sobre .NET, MVVM con CommunityToolkit, sin librerías de gráficas (Canvas + Polyline propios). Núcleo y acceso a datos sin dependencias de UI, listos para Uno Platform (Android/iOS/macOS/Linux/Web) sin cambios.", "Native WinUI 3 on .NET, MVVM with CommunityToolkit, no chart libraries (hand-rolled Canvas + Polyline). UI-free core and data layers, ready for Uno Platform (Android/iOS/macOS/Linux/Web) unchanged."),
        ["about.note"] = ("AVISO", "NOTE"),
        ["about.note.body"] = ("Proyecto no oficial, sin afiliación con SpaceX/Starlink. Los datos de cuenta (plan, facturación, soporte) solo existen en la app oficial con sesión iniciada y no se replican aquí por diseño.", "Unofficial project, not affiliated with SpaceX/Starlink. Account data (plan, billing, support) only exists in the signed-in official app and is not replicated here by design."),
        // Dish fleet (multi-endpoint switcher)
        ["dishes.title"] = ("ANTENAS", "DISHES"),
        ["dishes.note"] = ("Cada antena vive en su propio segmento con su IP. Añade una entrada por cada antena alcanzable desde esta red.", "Each dish lives on its own segment with its own IP. Add one entry per dish reachable from this network."),
        ["dishes.name"] = ("Nombre", "Name"),
        ["dishes.host"] = ("Anfitrión o IP", "Host or IP"),
        ["dishes.port"] = ("Puerto", "Port"),
        ["dishes.add"] = ("Añadir", "Add"),
        ["dishes.save"] = ("Guardar", "Save"),
        ["dishes.remove"] = ("Quitar", "Remove"),
        ["dishes.err.host"] = ("Escribe un anfitrión válido.", "Enter a valid host."),
        ["dishes.err.port"] = ("Puerto 1–65535.", "Port 1–65535."),
        ["dishes.err.name"] = ("Ese nombre ya existe.", "That name is taken."),
        ["dishes.err.last"] = ("Debe quedar al menos una antena.", "At least one dish must remain."),
        ["dish.default.name"] = ("Antena", "Dish"),
        ["obst.prolonged"] = ("Obstrucción prolongada media:", "Average prolonged obstruction:"),
        ["obst.every"] = ("cada", "every"),
        ["obst.prolonged.no"] = ("Sin registro de obstrucciones prolongadas", "No prolonged obstructions recorded"),
        ["align.tilt"] = ("Inclinación {0}°", "Tilt {0}°"),
        ["align.mast.suffix"] = (" · mástil no vertical", " · mast not vertical"),
        ["align.fixed"] = ("Antena fija: oriéntela una vez con la vista de alineación y déjela estática", "Fixed dish: align it once using the alignment view, then leave it static"),
        ["align.target"] = ("Objetivo {0}° / {1}°", "Target {0}° / {1}°"),
        ["align.dev"] = (" · desviación {0}°", " · off by {0}°"),
        ["misc.out.session"] = ("Sin cortes en esta sesión · corte más largo (antena): {0} s", "No outages this session · longest dish-side outage: {0} s"),
        ["misc.out.summary"] = ("{0} interrupciones · {1} en total", "{0} outages · {1} total"),
        ["spd.fail"] = ("No se pudo completar: ", "Couldn't complete: "),
        ["misc.updated"] = ("Actualizado", "Updated"),
        ["misc.connecting"] = ("Conectando…", "Connecting…"),
        ["misc.looking"] = ("Buscando la antena en tu red local", "Looking for the dish on your LAN"),
        ["misc.demo"] = ("Datos de demostración · antena inalcanzable en la red", "Demo data · dish unreachable on the LAN"),
        ["misc.plan"] = ("Residencial", "Residential"),
        ["misc.reach.no"] = ("Sin conexión directa (mostrando demo)", "No direct connection (showing demo)"),
        ["misc.reach.yes"] = ("Conexión directa activa", "Direct connection live"),
        ["misc.loc.gated"] = ("Ubicación restringida por el ajuste de privacidad de la antena", "Location restricted by the dish privacy setting"),
        ["misc.usage.no"] = ("Contadores no disponibles", "Counters unavailable"),
        ["misc.out.none"] = ("Sin interrupciones registradas", "No outages recorded"),
        ["state.connected"] = ("En línea", "Online"),
        ["state.searching"] = ("Buscando", "Searching"),
        ["state.booting"] = ("Iniciando", "Booting"),
        ["state.stowed"] = ("Guardada", "Stowed"),
        ["state.sleeping"] = ("En reposo", "Sleeping"),
        ["state.obstructed"] = ("Obstruida", "Obstructed"),
        ["state.nosats"] = ("Sin satélites", "No satellites"),
        ["state.nosignal"] = ("Sin señal", "No signal"),
        ["state.thermal"] = ("Apagado térmico", "Thermal shutdown"),
        ["state.offline"] = ("Sin conexión", "Offline"),
        ["state.unknown"] = ("Desconocido", "Unknown"),
        ["cause.obstructed"] = ("Obstruida", "Obstructed"),
        ["cause.nosats"] = ("Sin satélites", "No satellites"),
        ["cause.thermal"] = ("Apagado térmico", "Thermal shutdown"),
        ["cause.throttle"] = ("Límite térmico", "Thermal throttle"),
        ["cause.update"] = ("Actualización", "Update"),
        ["cause.network"] = ("Red", "Network"),
        ["cause.power"] = ("Energía", "Power"),
        ["cause.booting"] = ("Arranque", "Booting"),
        ["cause.stowed"] = ("Guardada", "Stowed"),
        ["cause.sleeping"] = ("Reposo", "Sleeping"),
        ["cause.skysearch"] = ("Búsqueda de cielo", "Sky search"),
        ["cause.actuator"] = ("Motores", "Actuators"),
        ["cause.cable"] = ("Prueba de cable", "Cable test"),
        ["cause.inhibited"] = ("Inhibida", "Inhibited"),
        ["cause.unknown"] = ("Desconocida", "Unknown"),
        // Dish-side strings (client + demo)
        ["dish.alert.motors"] = ("Motores bloqueados.", "Motors stuck."),
        ["dish.alert.throttle"] = ("Limitación por temperatura.", "Thermal throttling."),
        ["dish.alert.thermal"] = ("Apagado por temperatura.", "Thermal shutdown."),
        ["dish.alert.mast"] = ("El mástil no está en vertical.", "Mast is not vertical."),
        ["dish.alert.location"] = ("Ubicación inesperada.", "Unexpected location."),
        ["dish.alert.eth"] = ("Ethernet lento.", "Slow Ethernet."),
        ["dish.alert.eth100"] = ("Ethernet limitado a 100 Mbps.", "Ethernet capped at 100 Mbps."),
        ["dish.alert.noeth"] = ("Sin enlace Ethernet.", "No Ethernet link."),
        ["dish.alert.roaming"] = ("En itinerancia (roaming).", "Roaming."),
        ["dish.alert.heating"] = ("Calefactor de nieve activo.", "Snow heater active."),
        ["dish.alert.powersave"] = ("Ahorro de energía activo.", "Power saving active."),
        ["dish.alert.psu"] = ("Fuente de alimentación limitada por temperatura.", "Power supply thermally throttled."),
        ["dish.alert.lowmotor"] = ("Corriente de motor baja.", "Low motor current."),
        ["dish.alert.lowsignal"] = ("Señal menor de la prevista.", "Signal lower than predicted."),
        ["dish.alert.water"] = ("Agua detectada en la antena.", "Water detected in dish."),
        ["dish.alert.rwater"] = ("Agua detectada en el router.", "Water detected in router."),
        ["dish.alert.install"] = ("Instalación pendiente.", "Install pending."),
        ["dish.alert.mapreset"] = ("Mapa de obstrucciones reiniciado.", "Obstruction map reset."),
        ["dish.alert.stale"] = ("Telemetría desactualizada.", "Stale telemetry."),
        ["dish.alert.swreboot"] = ("Reinicio por actualización.", "Update reboot."),
        ["dish.alert.lowpower"] = ("Potencia de entrada baja.", "Low input power."),
        ["dish.demo.align"] = ("Acimut {0}° · Elevación {1}°", "Azimuth {0}° · Elevation {1}°"),
        ["dish.power.measured"] = ("Medido por la antena", "Measured by the dish"),
        ["dish.power.none"] = ("Este hardware no informa potencia", "This hardware reports no power"),
        ["dish.power.est"] = ("Mini típico en CC (simulado)", "Typical Mini on DC (simulated)"),
        ["dish.update.ok"] = ("Actualizado · sin reinicios pendientes", "Up to date · no reboots pending"),
        ["dish.update.ready"] = ("Actualización lista", "Update ready"),
        ["dish.update.fetch"] = ("Descargando actualización", "Downloading update"),
        ["dish.update.write"] = ("Instalando actualización", "Installing update"),
        ["dish.update.fail"] = ("Error de actualización", "Update failed"),
        ["dish.update.disabled"] = ("Actualizaciones desactivadas", "Updates disabled"),
        ["dish.update.reboot"] = ("reinicio pendiente", "reboot pending"),
        ["dish.selftest"] = ("Autoprueba", "Self-test"),
        ["dish.disablement"] = ("Restricción de servicio", "Service restriction"),
        ["dish.batt.usbc"] = ("USB-C", "USB-C"),
        ["dish.batt.battery"] = ("batería", "battery"),
        ["dish.batt.both"] = ("USB-C + batería", "USB-C + battery"),
        ["dish.batt.unknown"] = ("desconocida", "unknown"),
        ["dish.batt.charging"] = ("cargando", "charging"),
        ["dish.align.mast"] = ("El mástil no está en vertical. Nivele el equipo Starlink.", "The mast is not vertical. Level your Starlink."),
        ["dish.align.motors"] = ("Motores bloqueados. Revise el equipo Starlink.", "Motors stuck. Check your Starlink."),
        ["dish.notice.mis"] = ("Antena Starlink desalineada por {0}°.", "Starlink dish misaligned by {0}°."),
        ["dish.demo.notice"] = ("Antena Starlink desalineada por 20°.", "Starlink dish misaligned by 20°."),
        ["dish.demo.obst"] = ("Obstrucción breve detectada (cuña NO).", "Brief obstruction detected (NW wedge)."),
    };

    public static string Current { get; private set; } = "es";

    public static event Action? Changed;

    public static string Get(string key)
    {
        if (!S.TryGetValue(key, out var v)) return key;
        return Current == "en" ? v.En : v.Es;
    }

    public static string Get(string key, params object[] args)
    {
        try { return string.Format(Get(key), args); }
        catch { return Get(key); }
    }

    public static void Set(string lang)
    {
        if (lang != "es" && lang != "en") return;
        if (Current == lang) return;
        Current = lang;
        Changed?.Invoke();
    }

    public static string Duration(TimeSpan d)
        => d.TotalHours >= 1 ? Get("dur.h", (int)d.TotalHours, d.Minutes)
            : d.TotalMinutes >= 1 ? Get("dur.m", d.Minutes, d.Seconds)
            : Get("dur.s", d.Seconds);

    public static string Uptime(TimeSpan u)
        => u.TotalDays >= 1 ? Get("dur.d", (int)u.TotalDays, u.Hours) : Duration(u);

    private static readonly Dictionary<string, (string Es, string En)> Extra = new()
    {
        ["dur.h"] = ("{0} h {1} min", "{0} h {1} min"),
        ["dur.m"] = ("{0} min {1} s", "{0} min {1} s"),
        ["dur.s"] = ("{0} s", "{0} s"),
        ["dur.d"] = ("{0} d {1} h", "{0} d {1} h"),
    };

    static Text()
    {
        foreach (var kv in Extra) S[kv.Key] = kv.Value;
    }
}
