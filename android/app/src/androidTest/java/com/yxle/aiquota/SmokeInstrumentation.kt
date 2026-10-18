package com.yxle.aiquota

import android.app.Instrumentation
import android.content.Context
import android.content.ContextWrapper
import android.content.Intent
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.EditText
import android.os.Bundle
import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant

// Dependency-free, on-device integration tests. Uses isolated preferences and
// Android Keystore; never deletes the user's real pairing/cache preferences.
class SmokeInstrumentation : Instrumentation() {
    private lateinit var options: Bundle
    override fun onCreate(arguments: Bundle?) { super.onCreate(arguments); options = arguments ?: Bundle(); start() }
    override fun onStart() {
        val result = Bundle()
        try {
            val context = object : ContextWrapper(targetContext) {
                override fun getSharedPreferences(name: String, mode: Int) = super.getSharedPreferences("test_$name", mode)
            }
            val store = QuotaStore(context)
            store.prefs.edit().clear().commit()
            check(runCatching { Pairing.parse("http://example.com") }.isFailure)
            check(runCatching { Pairing.parse("aqm1:malformed") }.isFailure)
            check(store.remaining(null) == "N/A")
            check(store.remaining(JSONObject().put("usedPercent", 60).put("resetsAt", Instant.now().epochSecond + 3600)) == "40%")
            check(store.remaining(JSONObject().put("usedPercent", 0).put("resetsAt", Instant.now().epochSecond - 1)) == "N/A")
            check(store.stale(JSONObject().put("observedAt", Instant.now().epochSecond - 181)))
            check(store.stale(JSONObject().put("observedAt", Instant.now().epochSecond + 120)))
            val code = options.getString("pairing")
            if (code != null) {
                val pairing = Pairing.parse(code)
                val report = pairing.fetch()
                check(report.getInt("schemaVersion") == 1)
                check(report.getJSONObject("account").getString("product") == "codex")
                check(runCatching { pairing.copy(pin = "0".repeat(64)).fetch() }.isFailure) { "Certificate pinning accepted a wrong pin" }
                check(runCatching { pairing.copy(token = "A".repeat(43)).fetch() }.isFailure) { "Bridge accepted a wrong token" }
                store.add(code)
                check(store.codes().single() == code)
                check(!store.prefs.getString("connections", "")!!.contains(pairing.token))
                store.saveReports(listOf(report))
                check(store.accounts().size == 1)
                store.unpairAll()
                check(store.codes().isEmpty() && store.accounts().isEmpty())
                if (options.getString("seedUi") == "true") {
                    check(android.os.Build.HARDWARE in listOf("ranchu", "goldfish")) { "UI seeding is restricted to the emulator" }
                    val activity = startActivitySync(Intent(targetContext, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
                    fun descendants(view: View): List<View> = listOf(view) + if (view is ViewGroup) (0 until view.childCount).flatMap { descendants(view.getChildAt(it)) } else emptyList()
                    runOnMainSync {
                        descendants(activity.window.decorView).filterIsInstance<Button>().first { it.text == "Connections" }.performClick()
                        descendants(activity.window.decorView).filterIsInstance<EditText>().single().setText(code)
                        descendants(activity.window.decorView).filterIsInstance<Button>().first { it.text == "Pair PC" }.performClick()
                    }
                    check(QuotaStore(targetContext).codes().contains(code)) { "Actual pairing form did not save" }
                    runOnMainSync { activity.finish() }
                    sendStatus(0, Bundle().apply { putString("stream", "PASS: actual pairing form saved a PC profile.\n") })
                }
            }
            store.prefs.edit().clear().commit()
            result.putString("stream", "PASS: invalid pairing, unknown/expired quotas, staleness, TLS pinning, bearer authentication, Keystore round trip, cache, and unpairing.\n")
            finish(-1, result)
        } catch (error: Throwable) {
            result.putString("stream", "FAIL: ${error.javaClass.simpleName}: ${error.message}\n")
            finish(0, result)
        }
    }
}
