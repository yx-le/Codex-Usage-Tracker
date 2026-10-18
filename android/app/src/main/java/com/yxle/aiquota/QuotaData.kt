package com.yxle.aiquota

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import org.json.JSONArray
import org.json.JSONObject
import java.net.URI
import java.security.KeyStore
import java.security.MessageDigest
import java.security.cert.X509Certificate
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.net.ssl.*

data class Pairing(val host: String, val port: Int, val pin: String, val token: String) {
    companion object {
        fun parse(code: String): Pairing {
            require(code.trim().startsWith("aqm1:")) { "Paste an aqm1 pairing code from the PC bridge." }
            val json = JSONObject(String(Base64.decode(code.trim().substring(5), Base64.URL_SAFE or Base64.NO_WRAP)))
            val host = json.getString("host")
            val parts = host.split('.').map { it.toIntOrNull() ?: -1 }
            require(parts.size == 4 && parts.all { it in 0..255 } &&
                (parts[0] == 10 || parts[0] == 127 || (parts[0] == 192 && parts[1] == 168) || (parts[0] == 172 && parts[1] in 16..31))) { "Use the PC's private IPv4 address on your Wi-Fi network." }
            val port = json.getInt("port")
            val pin = json.getString("pin").uppercase()
            val token = json.getString("token")
            require(port in 1024..65535 && pin.matches(Regex("[A-F0-9]{64}")) && token.matches(Regex("[A-Za-z0-9_-]{43}"))) { "Invalid pairing code." }
            return Pairing(host, port, pin, token)
        }
    }
    fun fetch(): JSONObject {
        val expectedPin = pin
        val trust = object : X509TrustManager {
            override fun getAcceptedIssuers(): Array<X509Certificate> = emptyArray()
            override fun checkClientTrusted(chain: Array<X509Certificate>, authType: String) = throw java.security.cert.CertificateException("Client certificates unsupported")
            override fun checkServerTrusted(chain: Array<X509Certificate>, authType: String) {
                require(chain.isNotEmpty()) { "Missing bridge certificate" }
                chain[0].checkValidity()
                val actual = MessageDigest.getInstance("SHA-256").digest(chain[0].encoded).joinToString("") { "%02X".format(it) }
                if (!MessageDigest.isEqual(actual.toByteArray(), expectedPin.toByteArray())) throw java.security.cert.CertificateException("Bridge certificate changed. Pair again from your PC.")
            }
        }
        val ssl = SSLContext.getInstance("TLS").apply { init(null, arrayOf(trust), null) }
        val connection = URI("https://$host:$port/v1/status").toURL().openConnection() as HttpsURLConnection
        connection.sslSocketFactory = ssl.socketFactory
        // Exact host is part of the out-of-band pairing; trust is pinned to the
        // exact self-signed certificate, never to arbitrary system certificates.
        connection.hostnameVerifier = HostnameVerifier { hostname, _ -> hostname == host }
        connection.instanceFollowRedirects = false
        connection.connectTimeout = 5000; connection.readTimeout = 5000
        connection.setRequestProperty("Authorization", "Bearer $token")
        try {
            require(connection.responseCode == 200) { "PC rejected the connection. Check or renew pairing." }
            val bytes = connection.inputStream.use { it.readNBytes(1024 * 1024 + 1) }
            require(bytes.size <= 1024 * 1024) { "Bridge response too large" }
            return JSONObject(String(bytes, Charsets.UTF_8)).also { require(it.getInt("schemaVersion") == 1) { "Update the app to match this bridge." } }
        } finally { connection.disconnect() }
    }
}

class QuotaStore(val context: Context) {
    companion object { val pairingLock = Any() }
    val prefs = context.getSharedPreferences("quota", Context.MODE_PRIVATE)
    private fun key(): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getKey("pairing", null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").apply {
            init(KeyGenParameterSpec.Builder("pairing", KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build())
        }.generateKey()
    }
    private fun encrypt(value: String): String {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.ENCRYPT_MODE, key()) }
        return Base64.encodeToString(cipher.iv + cipher.doFinal(value.toByteArray()), Base64.NO_WRAP)
    }
    private fun decrypt(value: String): String {
        val bytes = Base64.decode(value, Base64.NO_WRAP)
        return String(Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, bytes.copyOfRange(0, 12))) }.doFinal(bytes.copyOfRange(12, bytes.size)))
    }
    fun codes(): List<String> = runCatching {
        val array = JSONArray(decrypt(prefs.getString("connections", "")!!))
        (0 until array.length()).map { array.getString(it) }
    }.getOrDefault(emptyList())
    fun add(code: String) = synchronized(pairingLock) {
        Pairing.parse(code)
        val existing = codes().filter { Pairing.parse(it).let { p -> p.host != Pairing.parse(code).host || p.port != Pairing.parse(code).port } }
        require(existing.size < 8) { "Up to eight PC profiles can be paired." }
        check(prefs.edit().putString("connections", encrypt(JSONArray(existing + code.trim()).toString())).commit()) { "Pairing could not be saved" }
    }
    fun unpairAll() = synchronized(pairingLock) { prefs.edit().remove("connections").remove("cache").remove("account").commit() }
    fun reports(): List<JSONObject> = runCatching { val a = JSONArray(prefs.getString("cache", "[]")); (0 until a.length()).map { a.getJSONObject(it) } }.getOrDefault(emptyList())
    fun saveReports(reports: List<JSONObject>) { prefs.edit().putString("cache", JSONArray(reports).toString()).apply() }
    fun accounts(): List<JSONObject> = reports().filter { Instant.now().epochSecond - it.optLong("generatedAt") < 7 * 86400 }.mapNotNull { it.optJSONObject("account") }
    fun selected(): JSONObject? {
        val product = prefs.getString("product", "codex")
        val available = accounts().filter { it.optString("product") == product }
        return available.find { it.optString("id") == prefs.getString("account", "") } ?: available.firstOrNull()
    }
    fun window(account: JSONObject?, id: String): JSONObject? {
        val a = account?.optJSONArray("windows") ?: return null
        return (0 until a.length()).map { a.getJSONObject(it) }.find { it.optString("id") == id }
    }
    fun remaining(w: JSONObject?): String {
        if (w == null || w.isNull("usedPercent") || (w.optLong("resetsAt", Long.MAX_VALUE) <= Instant.now().epochSecond)) return "N/A"
        return "${(100 - w.getDouble("usedPercent")).coerceIn(0.0, 100.0).toInt()}%"
    }
    fun stale(a: JSONObject?): Boolean = a == null || Instant.now().epochSecond - a.optLong("observedAt") > 180 || a.optLong("observedAt") > Instant.now().epochSecond + 60
    fun reset(w: JSONObject?): String {
        val epoch = w?.optLong("resetsAt", 0) ?: 0
        if (epoch <= 0) return "Reset time unavailable"
        val seconds = epoch - Instant.now().epochSecond
        val countdown = if (seconds <= 0) "Reset due · awaiting update" else "Resets in ${seconds / 3600}h ${(seconds % 3600) / 60}m"
        return countdown + "\n" + DateTimeFormatter.ofPattern("EEE, d MMM · HH:mm O").withZone(ZoneId.systemDefault()).format(Instant.ofEpochSecond(epoch))
    }
    fun lastUpdate(a: JSONObject?): String = if (a == null) "No reading yet" else "Last reading " + DateTimeFormatter.ofPattern("d MMM · HH:mm").withZone(ZoneId.systemDefault()).format(Instant.ofEpochSecond(a.optLong("observedAt")))
}

object QuotaSync {
    private val worker = Executors.newSingleThreadExecutor()
    private val busy = AtomicBoolean(false)
    fun refresh(context: Context, done: (String) -> Unit) {
        if (!busy.compareAndSet(false, true)) { done("Already refreshing"); return }
        val appContext = context.applicationContext
        worker.execute {
            var message: String
            try {
                val store = QuotaStore(appContext)
                val reports = store.reports().toMutableList()
                var successes = 0
                val codes = store.codes()
                for (code in codes) {
                    runCatching { Pairing.parse(code).fetch() }.onSuccess { report ->
                        reports.removeAll { it.optString("collectorId") == report.getString("collectorId") }
                        reports.add(report); successes++
                    }
                }
                // Retain offline readings, but never pretend cached values are fresh.
                synchronized(QuotaStore.pairingLock) {
                    if (store.codes() == codes) store.saveReports(reports.filter { Instant.now().epochSecond - it.optLong("generatedAt") < 7 * 86400 })
                }
                message = when { codes.isEmpty() -> "Pair your PC to receive usage"; successes == codes.size -> "Updated from PC"; successes > 0 -> "Some PCs are offline"; else -> "PC unreachable or pairing rejected · cached readings retained" }
            } catch (_: Exception) { message = "Could not refresh · cached readings retained" }
            finally { busy.set(false) }
            android.os.Handler(android.os.Looper.getMainLooper()).post { done(message) }
        }
    }
}
