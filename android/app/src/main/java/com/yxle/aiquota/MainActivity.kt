package com.yxle.aiquota

import android.Manifest
import android.app.Activity
import android.app.AlertDialog
import android.content.Intent
import android.content.res.Configuration
import android.graphics.*
import android.graphics.drawable.GradientDrawable
import android.net.Uri
import android.os.*
import android.provider.Settings
import android.view.*
import android.widget.*
import org.json.JSONObject
import java.time.Instant

class MainActivity : Activity() {
    private lateinit var store: QuotaStore
    private lateinit var body: LinearLayout
    private var page = 0
    private var status = "Your AI subscriptions, at a glance"
    private val handler = Handler(Looper.getMainLooper())
    private val preferenceListener = android.content.SharedPreferences.OnSharedPreferenceChangeListener { _, key ->
        if (key == "notch") handler.post { if (!isDestroyed) render() }
    }
    private val ticker = object : Runnable {
        override fun run() { refresh(); handler.postDelayed(this, 60_000) }
    }
    private var pendingNotch = false
    private val dark get() = resources.configuration.uiMode and Configuration.UI_MODE_NIGHT_MASK == Configuration.UI_MODE_NIGHT_YES
    private val background get() = Color.parseColor(if (dark) "#101923" else "#F2F6F8")
    private val ink get() = Color.parseColor(if (dark) "#F4F8FA" else "#142C3A")
    private val muted get() = Color.parseColor(if (dark) "#A5B6C2" else "#526875")
    private val accent get() = Color.parseColor(if (dark) "#54DEC0" else "#007B66")
    private fun dp(value: Int) = (value * resources.displayMetrics.density).toInt()
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState); store = QuotaStore(this)
        if (savedInstanceState != null) page = savedInstanceState.getInt("page")
        render()
    }
    override fun onSaveInstanceState(out: Bundle) { super.onSaveInstanceState(out); out.putInt("page", page) }
    override fun onResume() {
        super.onResume()
        store.prefs.registerOnSharedPreferenceChangeListener(preferenceListener)
        if (!NotchService.running) store.prefs.edit().putBoolean("notch", false).apply()
        handleNotchRequest()
        if (pendingNotch && Settings.canDrawOverlays(this)) { pendingNotch = false; startNotch() }
        render(); handler.post(ticker)
    }
    override fun onNewIntent(intent: Intent) { super.onNewIntent(intent); setIntent(intent); handleNotchRequest(); render() }
    private fun handleNotchRequest() {
        if (!intent.getBooleanExtra("enable_notch", false) && intent.action != "com.yxle.aiquota.ENABLE_NOTCH") return
        intent.removeExtra("enable_notch"); intent.action = Intent.ACTION_MAIN; page = 1
        if (Settings.canDrawOverlays(this)) startNotch()
        else { pendingNotch = true; startActivity(Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION, Uri.parse("package:$packageName"))) }
    }
    override fun onPause() { store.prefs.unregisterOnSharedPreferenceChangeListener(preferenceListener); handler.removeCallbacks(ticker); super.onPause() }
    private fun refresh() { QuotaSync.refresh(this) { if (!isDestroyed) { status = it; if (page == 0) render() } } }
    private fun text(value: String, size: Float = 14f, color: Int = ink, bold: Boolean = false) = TextView(this).apply {
        text = value; textSize = size; setTextColor(color)
        if (bold) typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
        setPadding(0, dp(3), 0, dp(3))
    }
    private fun stack() = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
    private fun button(label: String, action: () -> Unit) = Button(this).apply {
        text = label; isAllCaps = false; setTextColor(ink); textSize = 13f
        elevation = 0f; stateListAnimator = null
        background = shape(if (dark) Color.parseColor("#293B48") else Color.WHITE, 14)
        setOnClickListener { action() }
        layoutParams = LinearLayout.LayoutParams(-1, dp(48)).apply { topMargin = dp(10) }
    }
    private fun shape(color: Int, radius: Int) = GradientDrawable().apply { setColor(color); cornerRadius = dp(radius).toFloat() }
    private fun card(): LinearLayout = stack().apply {
        setPadding(dp(18), dp(16), dp(18), dp(16))
        background = shape(if (dark) Color.parseColor("#1D2D39") else Color.WHITE, 22)
        layoutParams = LinearLayout.LayoutParams(-1, -2).apply { topMargin = dp(14) }
        body.addView(this)
    }
    private fun toggle(parent: LinearLayout, label: String, key: String, initial: Boolean, action: ((Boolean) -> Unit)? = null) {
        val control = Switch(this).apply {
            text = label; setTextColor(ink); textSize = 15f; isChecked = store.prefs.getBoolean(key, initial)
            setPadding(0, dp(8), 0, dp(8)); minimumHeight = dp(48)
            setOnCheckedChangeListener { _, checked -> store.prefs.edit().putBoolean(key, checked).apply(); action?.invoke(checked) }
        }
        parent.addView(control)
    }
    private fun render() {
        val root = stack().apply { setBackgroundColor(this@MainActivity.background) }
        root.setOnApplyWindowInsetsListener { view, insets ->
            val bars = insets.getInsets(WindowInsets.Type.systemBars() or WindowInsets.Type.displayCutout())
            view.setPadding(dp(20) + bars.left, dp(10) + bars.top, dp(20) + bars.right, dp(8) + bars.bottom); insets
        }
        root.addView(text("AI Quota", 30f, bold = true))
        root.addView(text(status, 12f, muted))
        val nav = LinearLayout(this)
        listOf("Overview", "Display", "Connections").forEachIndexed { index, name ->
            nav.addView(button(name) { page = index; render() }.apply {
                setTextColor(if (page == index) accent else muted)
                layoutParams = LinearLayout.LayoutParams(0, dp(44), 1f).apply { setMargins(dp(2), dp(10), dp(2), dp(8)) }
            })
        }
        root.addView(nav)
        body = stack().apply { setPadding(0, 0, 0, dp(24)) }
        root.addView(ScrollView(this).apply { isFillViewport = true; addView(body) }, LinearLayout.LayoutParams(-1, 0, 1f))
        when (page) { 0 -> overview(); 1 -> display(); else -> connections() }
        setContentView(root)
        val lightBars = WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS or WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS
        window.insetsController?.setSystemBarsAppearance(if (dark) 0 else lightBars, lightBars)
    }
    private fun overview() {
        val chips = LinearLayout(this)
        val selected = store.prefs.getString("product", "codex")
        listOf("codex" to "Codex", "claude" to "Claude", "gemini" to "Gemini").forEach { (id, label) ->
            chips.addView(button(label) {
                store.prefs.edit().putString("product", id).remove("account").apply(); render()
            }.apply {
                setTextColor(if (id == selected) accent else muted)
                layoutParams = LinearLayout.LayoutParams(0, dp(44), 1f).apply { setMargins(dp(3), dp(6), dp(3), 0) }
            })
        }
        body.addView(chips)
        val accounts = store.accounts().filter { it.optString("product") == selected }
        if (accounts.size > 1) {
            val current = store.selected()?.optString("id")
            val picker = Spinner(this)
            picker.adapter = ArrayAdapter(this, android.R.layout.simple_spinner_dropdown_item, accounts.map { it.optString("name") })
            picker.setSelection(accounts.indexOfFirst { it.optString("id") == current }.coerceAtLeast(0))
            picker.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
                override fun onNothingSelected(parent: AdapterView<*>?) {}
                override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                    if (accounts[position].optString("id") != store.selected()?.optString("id")) { store.prefs.edit().putString("account", accounts[position].optString("id")).apply(); render() }
                }
            }; body.addView(picker)
        }
        val account = store.selected()
        if (account == null) {
            card().apply {
                addView(text(if (selected == "codex") "Connect your PC" else "Connector not available yet", 20f, bold = true))
                addView(text(if (selected == "codex") "Pair the Windows bridge on the same Wi-Fi network to see your real Codex usage." else "App switching is available. Automatic $selected quota monitoring is not connected; no usage values are estimated.", 14f, muted))
                addView(button(if (selected == "codex") "Pair a PC" else "Open app") { if (selected == "codex") { page = 2; render() } else AppLauncher.open(this@MainActivity, selected!!) })
            }
        } else {
            body.addView(text(account.optString("name", "PC profile"), 20f, bold = true).apply { setPadding(0, dp(20), 0, 0) })
            body.addView(text((if (store.stale(account)) "STALE · " else "") + store.lastUpdate(account), 12f, muted))
            listOf("fiveHour" to "5-hour", "weekly" to "Weekly").forEach { (id, title) ->
                val window = store.window(account, id)
                card().apply {
                    addView(text(title.uppercase(), 12f, muted, true))
                    addView(text(store.remaining(window), 40f, bold = true))
                    addView(ProgressBar(this@MainActivity, null, android.R.attr.progressBarStyleHorizontal).apply {
                        max = 100; progress = store.remaining(window).removeSuffix("%").toIntOrNull() ?: 0
                        progressTintList = android.content.res.ColorStateList.valueOf(if (progress <= 10) Color.rgb(255, 113, 140) else if (progress <= 25) Color.rgb(255, 192, 92) else if (id == "fiveHour") accent else Color.rgb(153, 165, 235))
                    }, LinearLayout.LayoutParams(-1, dp(5)).apply { topMargin = dp(6); bottomMargin = dp(10) })
                    addView(text(store.reset(window), 12f, muted))
                }
            }
            card().apply { addView(text("Last 7 days", 15f, bold = true)); addView(HistoryView(this@MainActivity, account), LinearLayout.LayoutParams(-1, dp(115))); addView(text("Teal: 5-hour   ·   Lavender: weekly", 11f, muted)) }
        }
        body.addView(button("Refresh from PC") { refresh() })
        body.addView(button("Open ${if (selected == "codex") "ChatGPT" else selected}") { AppLauncher.open(this, selected!!) })
    }
    private fun display() {
        body.addView(text("A quieter way to check usage", 22f, bold = true))
        card().apply {
            toggle(this, "Show notch display", "notch", false) { enabled ->
                if (enabled) {
                    if (!Settings.canDrawOverlays(this@MainActivity)) {
                        store.prefs.edit().putBoolean("notch", false).apply(); pendingNotch = true
                        startActivity(Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION, Uri.parse("package:$packageName")))
                    } else startNotch()
                } else stopService(Intent(this@MainActivity, NotchService::class.java))
            }
            addView(text("Tap the pill for usage and app switching. Long-press to hide. Use the AI Quota Quick Settings tile to toggle it later.", 12f, muted))
        }
        card().apply {
            addView(text("App toggle", 17f, bold = true))
            addView(text("Choose which app shortcuts appear in the expanded pill.", 12f, muted))
            toggle(this, "ChatGPT / Codex", "shortcut_codex", true)
            toggle(this, "Claude", "shortcut_claude", true)
            toggle(this, "Gemini", "shortcut_gemini", true)
        }
        card().apply {
            addView(text("Pill position", 17f, bold = true))
            addView(text("Fits below the camera and system status bar. Adjust for your Xiaomi 17 Pro's display settings.", 12f, muted))
            val value = text("Vertical offset: ${store.prefs.getInt("offset", 0)} dp", 13f)
            addView(value)
            addView(SeekBar(this@MainActivity).apply { max = 100; progress = store.prefs.getInt("offset", 0); setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
                override fun onProgressChanged(bar: SeekBar?, progress: Int, fromUser: Boolean) { if (fromUser) { store.prefs.edit().putInt("offset", progress).apply(); value.text = "Vertical offset: $progress dp" } }
                override fun onStartTrackingTouch(bar: SeekBar?) {}
                override fun onStopTrackingTouch(bar: SeekBar?) {}
            }) })
        }
        body.addView(button("HyperOS app permissions") { startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:$packageName"))) })
        body.addView(text("If HyperOS stops the pill, check floating-window permission and the app's battery settings. The rear display is not controlled by this app.", 12f, muted))
    }
    private fun startNotch() {
        if (Build.VERSION.SDK_INT >= 33 && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != android.content.pm.PackageManager.PERMISSION_GRANTED)
            requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), 10)
        try {
            startForegroundService(Intent(this, NotchService::class.java)); store.prefs.edit().putBoolean("notch", true).apply()
        } catch (_: Exception) { store.prefs.edit().putBoolean("notch", false).apply(); Toast.makeText(this, "Could not show the pill. Check floating-window permission.", Toast.LENGTH_LONG).show() }
    }
    private fun connections() {
        body.addView(text("Pair your Windows tracker", 22f, bold = true))
        body.addView(text("Run the PC bridge, then paste its pairing code. Keep both devices on the same private Wi-Fi.", 14f, muted))
        card().apply {
            addView(text("Secure pairing code", 16f, bold = true))
            val input = EditText(this@MainActivity).apply {
                hint = "aqm1:…"; setTextColor(ink); setHintTextColor(muted)
                inputType = android.text.InputType.TYPE_CLASS_TEXT or android.text.InputType.TYPE_TEXT_VARIATION_PASSWORD
                isSingleLine = true; importantForAutofill = View.IMPORTANT_FOR_AUTOFILL_NO
                transformationMethod = android.text.method.PasswordTransformationMethod.getInstance()
            }
            addView(input)
            addView(button("Pair PC") {
                try { store.add(input.text.toString()); input.text.clear(); status = "Pairing saved · checking PC"; page = 0; render(); refresh() }
                catch (_: Exception) { Toast.makeText(this@MainActivity, "Invalid or incomplete pairing code. Copy the full aqm1 code from the PC.", Toast.LENGTH_LONG).show() }
            })
            addView(text("The PC's certificate is pinned. Pairing secrets use Android Keystore encryption; provider passwords and chat content stay off the phone.", 12f, muted))
        }
        card().apply {
            addView(text("${store.codes().size} PC profile(s) paired", 17f, bold = true))
            addView(text("Multiple PCs/profiles can be paired. Only the active Codex profile's quota is available from each PC; other provider connectors are planned.", 12f, muted))
            addView(button("Forget all paired PCs") {
                AlertDialog.Builder(this@MainActivity).setTitle("Forget paired PCs?").setMessage("Remove pairing keys and cached readings from this phone. Revoke the old code on the PC to prevent reuse.")
                    .setNegativeButton("Cancel", null).setPositiveButton("Forget") { _, _ -> store.unpairAll(); status = "Pairings removed"; render() }.show()
            })
        }
        body.addView(text("Prototype for Xiaomi 17 Pro / HyperOS. Monitoring is local-network only. A sleeping or unreachable PC leaves a timestamped cached reading. No cloud service or analytics.", 12f, muted))
    }
}

object AppLauncher {
    fun open(context: android.content.Context, product: String) {
        val pkg = when (product) { "claude" -> "com.anthropic.claude"; "gemini" -> "com.google.android.apps.bard"; else -> "com.openai.chatgpt" }
        val launch = context.packageManager.getLaunchIntentForPackage(pkg)
        if (launch == null) { Toast.makeText(context, "App is not installed or has no launcher on this device", Toast.LENGTH_LONG).show(); return }
        try { context.startActivity(launch.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) }
        catch (_: Exception) { Toast.makeText(context, "Android could not open this app", Toast.LENGTH_LONG).show() }
    }
}

class HistoryView(context: android.content.Context, private val account: JSONObject) : View(context) {
    override fun onDraw(canvas: Canvas) {
        val a = account.optJSONArray("history") ?: return
        val now = Instant.now().epochSecond; val from = now - 7 * 86400
        val paint = Paint(Paint.ANTI_ALIAS_FLAG).apply { strokeWidth = 2.5f; style = Paint.Style.STROKE }
        val height = height - 20f
        listOf("fiveRemaining" to Color.rgb(84, 222, 192), "weekRemaining" to Color.rgb(168, 181, 255)).forEach { (field, color) ->
            paint.color = color
            var last: PointF? = null; var reset: Long? = null
            for (i in 0 until a.length()) {
                val item = a.getJSONObject(i); val time = item.optLong("time")
                if (time !in from..now || item.isNull(field)) { last = null; continue }
                val point = PointF(((time - from).toDouble() / (7 * 86400) * width).toFloat(), (1 - item.getDouble(field).coerceIn(0.0, 100.0) / 100).toFloat() * height + 10)
                val currentReset = item.optLong(if (field == "fiveRemaining") "fiveReset" else "weekReset")
                last?.let { if (reset == currentReset) canvas.drawLine(it.x, it.y, point.x, point.y, paint) }
                last = point; reset = currentReset
            }
        }
    }
}
