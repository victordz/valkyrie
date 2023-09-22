package com.android.accessmomdata

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.content.SharedPreferences
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.util.Log
import android.view.View
import android.widget.TextView
import androidx.fragment.app.FragmentActivity
import androidx.documentfile.provider.DocumentFile
import com.github.k1rakishou.fsaf.FileChooser
import com.github.k1rakishou.fsaf.FileManager
import com.github.k1rakishou.fsaf.file.AbstractFile
import com.github.k1rakishou.fsaf.callback.FSAFActivityCallbacks
import com.github.k1rakishou.fsaf_app.AccessBaseDirectory
import java.io.File

class AccessActivity : Activity(), FSAFActivityCallbacks {

    private lateinit var fileManager: FileManager
    private lateinit var fileChooser: FileChooser

    private lateinit var sharedPreferences: SharedPreferences

    private val accessBaseDirectory = AccessBaseDirectory({
        getTreeUri()
    }, {
        null
    })

    companion object {
        private const val TAG = "MainActivity"

        const val DOCID_ANDROID_DATA = "primary:Android/data"

        const val MOM_DATA_DIR_NAME = "com.fantasyflightgames.mom"

        const val REQ_SAF_R_DATA = 2030

        const val TREE_URI = "tree_uri"
	
	@JvmStatic
    	fun makeActivity(act: Activity, appContext: Context) {
		Log.e(TAG, " running mack act")
                Log.e(TAG, " appCon: $appContext")
       		val intent = Intent(act, AccessActivity::class.java)
		Log.e(TAG, " done intent")
       		act.startActivity(intent)
    	}
    }

    var tv: TextView? = null

    fun doPermissionRequestAndCopy() {
        var docId = DOCID_ANDROID_DATA
        if (DocumentVM.atLeastR()) {
            if (Build.VERSION.SDK_INT > 31) {
                docId += "/" + MOM_DATA_DIR_NAME
            }
            Log.e(TAG, " onSelected: $docId")
            Log.e(TAG, " askForPerm onSelected: $docId")
            DocumentVM.requestFolderPermission(this@AccessActivity, REQ_SAF_R_DATA, docId)

        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
	    Log.e(TAG, " in onCreate")
        super.onCreate(savedInstanceState)
        //setContentView(R.layout.activity_main)

        sharedPreferences = getSharedPreferences("test", MODE_PRIVATE)

        fileManager = FileManager(applicationContext)
        fileChooser = FileChooser(applicationContext)
        //testSuite = TestSuite(fileManager, this)
        //fileChooser.setCallbacks(this)

        Log.e(TAG, "getTreeUri: ${getTreeUri()}")

        if (getTreeUri() != null) {
            fileManager.registerBaseDir(AccessBaseDirectory::class.java, accessBaseDirectory)
        }

	    doPermissionRequestAndCopy()



    }

    @Synchronized
    private fun goSAF(uri: Uri, docId: String? = null, hide: Boolean? = false) {
        // read and write Storage Access Framework https://developer.android.com/guide/topics/providers/document-provider
        val root = DocumentFile.fromTreeUri(this, uri);
        // make a new dir
        //val dir = root?.createDirectory("test")

        //list children
        root?.listFiles()?.let {
            val sb = StringBuilder()
            for (documentFile in it) {
                if(documentFile.isDirectory){
                    sb.append("📁")
                }
                sb.append(documentFile.name).append('\n')
            }
            tv?.text = sb.toString()
        }
    }

    private fun goAndroidData(path: String?) {
        val uri = DocumentVM.getFolderUri(DOCID_ANDROID_DATA, true)
        goSAF(uri, path)
    }


    override fun onActivityResult(requestCode: Int, resultCode: Int, intent: Intent?) {
        val act: Activity? = this
        val data = intent?.data
        if (requestCode == REQ_SAF_R_DATA) {
            if (act != null) {
                if (!DocumentVM.checkFolderPermission(act,DOCID_ANDROID_DATA)) {
                    if (resultCode == Activity.RESULT_OK) {
                        if (data != null) {
                            goSAF(data)

                            removeTreeUri()
                            storeTreeUri(data)

                            try {
                                val flags = Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                                contentResolver.takePersistableUriPermission(data, flags)
                            } catch (e: Exception) {
                                e.printStackTrace()
                            }

                            copyMoMData()
                        }
                    } 
                } else {
                    goAndroidData(null)
                }
            }
        }
        Log.i(TAG, "End of onActivityResult")
        super.onActivityResult(requestCode, resultCode, intent)
        finish()
    }


    private fun copyMoMData() {
        try {
            val baseSAFDir = fileManager.newBaseDirectoryFile<AccessBaseDirectory>()
            if (baseSAFDir == null) {
                throw NullPointerException("baseSAFDir is null!")
            }

            val baseFileApiDir = fileManager.fromRawFile(
                File(Environment.getExternalStorageDirectory().absolutePath + "/Valkyrie" , MOM_DATA_DIR_NAME)
            )
            val directory = File(Environment.getExternalStorageDirectory().absolutePath + "/Valkyrie" , MOM_DATA_DIR_NAME)

            if (!directory.exists()) {
                directory.mkdir();
            }

            if (baseSAFDir.getFullPath().endsWith(MOM_DATA_DIR_NAME)) {
                fileManager.copyDirectoryWithContent(baseSAFDir, baseFileApiDir, true)
            }
            else {
                val innerFiles = fileManager.listFiles(baseSAFDir)
            
                innerFiles.forEach {
                    if (it.getFullPath().endsWith(MOM_DATA_DIR_NAME)) {
                        Log.i(TAG, " from: $it to: $baseFileApiDir")

                        fileManager.copyDirectoryWithContent(it, baseFileApiDir, true)
                    }
                }
            }


	       val copyCompleteIndicationFile = File(Environment.getExternalStorageDirectory().absolutePath + "/Valkyrie/com.fantasyflightgames.mom/done");
           if (!copyCompleteIndicationFile.exists()) {
               copyCompleteIndicationFile.createNewFile()
           }

            val message = "Copy completed"

	        Log.d(TAG, message)
        } catch (error: Throwable) {
	        Log.d(TAG, Log.getStackTraceString(error))
        }
    }

    private fun storeTreeUri(uri: Uri) {
        val dir = checkNotNull(fileManager.fromUri(uri)) { "fileManager.fromUri(${uri}) failure" }

        check(fileManager.exists(dir)) { "Does not exist" }
        check(fileManager.isDirectory(dir)) { "Not a dir" }

        fileManager.registerBaseDir<AccessBaseDirectory>(accessBaseDirectory)
        sharedPreferences.edit().putString(TREE_URI, uri.toString()).apply()
        Log.d(TAG, "storeTreeUri: $uri")
    }

    private fun removeTreeUri() {
        val treeUri = getTreeUri()
        if (treeUri == null) {
            println("Already removed")
            return
        }

        fileChooser.forgetSAFTree(treeUri)
        fileManager.unregisterBaseDir<AccessBaseDirectory>()
        sharedPreferences.edit().remove(TREE_URI).apply()
    }

    private fun getTreeUri(): Uri? {
        return sharedPreferences.getString(TREE_URI, null)
            ?.let { str -> Uri.parse(str) }
    }

    override fun fsafStartActivityForResult(intent: Intent, requestCode: Int) {
        //
        Log.i(TAG, "fsafStartActivityForResult")
    }

}