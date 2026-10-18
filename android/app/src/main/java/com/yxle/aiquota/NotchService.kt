package com.yxle.aiquota

import android.app.*
import android.content.Intent
import android.content.pm.ServiceInfo
import android.graphics.Color
import android.graphics.PixelFormat
import android.graphics.drawable.GradientDrawable
import android.os.*
import android.provider.Settings
import android.service.quicksettings.Tile
import android.service.quicksettings.TileService
import android.view.*
import android.widget.*

class NotchService : Service() {
    companion object { @Volatile var running = false }
    private lateinit var store: QuotaStore
    private lateinit var manager: WindowManager
    private var view: LinearLayout? = null
    private var expanded = false
    private val handler = Handler(Looper.getMainLooper())
    private var loops = 0
    private val tick = object : Runnable {
        override fun run() {
            if (!Settings.canDrawOverlays(this@NotchService)) { stopSelf(); return }
            // No network polling while the screen is off; countdown text is local.
            val screenOn = getSystemService(PowerManager::class.java).isInteractive
            if (loops++ % 6 == 0 && screenOn) QuotaSync.refresh(this@NotchService) { if (view != null) draw() }
            if (screenOn) draw() else view?.visibility = View.GONE
            handler.postDelayed(this, 10_000)
        }
    }
    private fun dp(value: Int) = (value * resources.displayMetrics.density).toInt()
    override fun onCreate() {
        super.onCreate(); store = QuotaStore(this); manager = getSystemService(WindowManager::class.java)
    }
    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == "stop") { stopSelf(); return START_NOT_STICKY }
        if (!Settings.canDrawOverlays(this)) { stopSelf(); return START_NOT_STICKY }
        getSystemService(NotificationManager::class.java).createNotificationChannel(NotificationChannel("notch", "Quota notch", NotificationManager.IMPORTANCE_LOW))
        val open = PendingIntent.getActivity(this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
        val stop = PendingIntent.getService(this, 1, Intent(this, NotchService::class.java).setAction("stop"), PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
        val notification = Notification.Builder(this, "notch").setSmallIcon(com.yxle.aiquota.R.drawable.ic_quota)
            .setContentTitle("AI Quota notch is on").setContentText("Tap to open · switch off any time").setContentIntent(open).setOngoing(true)
            .addAction(Notification.Action.Builder(null, "Turn off", stop).build()).build()
        if (Build.VERSION.SDK_INT >= 34) startForeground(4, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE) else startForeground(4, notification)
        store.prefs.edit().putBoolean("notch", true).apply()
        running = true
        if (view == null) {
            view = LinearLayout(this).apply {
                orientation = LinearLayout.VERTICAL; gravity = Gravity.CENTER; elevation = dp(8).toFloat()
                background = GradientDrawable().apply { setColor(Color.argb(238, 13, 19, 26)); cornerRadius = dp(24).toFloat() }
                setPadding(dp(16), dp(5), dp(16), dp(5))
                setOnClickListener { expanded = !expanded; draw() }
                setOnLongClickListener { stopSelf(); true }
                contentDescription = "AI usage. Tap to expand, long press to turn off."
            }
            val params = WindowManager.LayoutParams(-2, -2, WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE, PixelFormat.TRANSLUCENT).apply { gravity = Gravity.TOP or Gravity.CENTER_HORIZONTAL; y = dp(store.prefs.getInt("offset", 0) + 6) }
            try { manager.addView(view, params) }
            catch (_: Exception) { stopSelf(); return START_NOT_STICKY }
        }
        handler.removeCallbacks(tick); handler.post(tick)
        return START_NOT_STICKY
    }
    private fun label(value: String, size: Float = 13f) = TextView(this).apply { text = value; textSize = size; setTextColor(Color.rgb(240, 247, 250)); gravity = Gravity.CENTER; setPadding(0, dp(6), 0, dp(6)) }
    private fun draw() {
        val root = view ?: return
        root.visibility = View.VISIBLE; root.removeAllViews()
        val product = store.prefs.getString("product", "codex") ?: "codex"
        val account = store.selected()
        val value = store.remaining(store.window(account, "fiveHour"))
        root.addView(label("${when(product) { "claude" -> "CL"; "gemini" -> "GM"; else -> "CX" }}  $value${if (account != null && store.stale(account)) " · stale" else ""}", 13f))
        if (expanded) {
            root.addView(label(account?.optString("name") ?: "No connected $product quota", 11f))
            if (account != null) root.addView(label("5h $value   ·   Week ${store.remaining(store.window(account, "weekly"))}", 12f))
            val shortcuts = LinearLayout(this)
            listOf("codex" to "ChatGPT", "claude" to "Claude", "gemini" to "Gemini").forEach { (id, name) ->
                if (store.prefs.getBoolean("shortcut_$id", true)) shortcuts.addView(label(name, 12f).apply {
                    setPadding(dp(12), dp(10), dp(12), dp(10)); minimumHeight = dp(44)
                    contentDescription = "Open $name and select its usage"
                    setOnClickListener { store.prefs.edit().putString("product", id).remove("account").apply(); expanded = false; draw(); AppLauncher.open(this@NotchService, id) }
                })
            }
            root.addView(shortcuts)
            root.addView(label("Open dashboard", 12f).apply { minimumHeight = dp(44); setOnClickListener { expanded = false; draw(); startActivity(Intent(this@NotchService, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) } })
        }
        val params = root.layoutParams as WindowManager.LayoutParams
        params.y = dp(store.prefs.getInt("offset", 0) + 6)
        try { manager.updateViewLayout(root, params) } catch (_: Exception) { stopSelf() }
    }
    override fun onBind(intent: Intent?) = null
    override fun onDestroy() {
        running = false
        handler.removeCallbacksAndMessages(null)
        view?.let { runCatching { manager.removeView(it) } }; view = null
        store.prefs.edit().putBoolean("notch", false).apply()
        stopForeground(STOP_FOREGROUND_REMOVE); super.onDestroy()
    }
}

class QuotaTile : TileService() {
    override fun onStartListening() {
        super.onStartListening(); qsTile?.apply { state = if (QuotaStore(this@QuotaTile).prefs.getBoolean("notch", false)) Tile.STATE_ACTIVE else Tile.STATE_INACTIVE; updateTile() }
    }
    override fun onClick() {
        super.onClick()
        val store = QuotaStore(this)
        if (store.prefs.getBoolean("notch", false)) {
            store.prefs.edit().putBoolean("notch", false).apply()
            stopService(Intent(this, NotchService::class.java)); onStartListening()
        } else {
            if (Settings.canDrawOverlays(this)) {
                try {
                    // A tile tap is an explicit user action. Once permission is
                    // granted, toggle the foreground overlay directly.
                    startForegroundService(Intent(this, NotchService::class.java))
                    store.prefs.edit().putBoolean("notch", true).apply()
                    qsTile?.apply { state = Tile.STATE_ACTIVE; updateTile() }
                    return
                } catch (_: IllegalStateException) { /* Fall back to the visible activity. */ }
                catch (_: SecurityException) { /* Ask through the visible activity. */ }
            }
            // Launching the visible activity keeps permission requests and service
            // startup in a user action, rather than relying on background exemptions.
            val launch = Intent(this, MainActivity::class.java).setAction("com.yxle.aiquota.ENABLE_NOTCH")
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP)
            startActivityAndCollapse(PendingIntent.getActivity(this, 2, launch, PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT))
        }
    }
}
