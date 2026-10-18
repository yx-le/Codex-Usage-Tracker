plugins { id("com.android.application") }
android {
    namespace = "com.yxle.aiquota"
    compileSdk { version = release(36) { minorApiLevel = 1 } }
    defaultConfig {
        applicationId = "com.yxle.aiquota"
        minSdk = 34
        targetSdk = 36
        versionCode = 1
        versionName = "0.1.0-alpha"
        testInstrumentationRunner = "com.yxle.aiquota.SmokeInstrumentation"
    }
    compileOptions { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
    buildTypes { release { isMinifyEnabled = false } }
}
