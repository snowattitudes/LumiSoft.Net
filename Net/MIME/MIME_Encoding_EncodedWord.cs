﻿using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Implements 'encoded-word' encoding. Defined in RFC 2047.
    /// </summary>
    public class MIME_Encoding_EncodedWord
    {
        private MIME_EncodedWordEncoding m_Encoding;
        private Encoding                 m_pCharset = null;
        private bool                     m_Split    = true;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="encoding">Encoding to use to encode text.</param>
        /// <param name="charset">Charset to use for encoding. If not sure UTF-8 is strongly recommended.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>charset</b> is null reference.</exception>
        public MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding encoding,Encoding charset)
        {
            if(charset == null){
                throw new ArgumentNullException("charset");
            }

            m_Encoding = encoding;
            m_pCharset = charset;
        }

                
        #region method Encode

        /// <summary>
        /// Encodes specified text if it contains 8-bit chars, otherwise text won't be encoded.
        /// </summary>
        /// <param name="text">Text to encode.</param>
        /// <returns>Returns encoded text.</returns>
        public string Encode(string text)
        {            
            if(MustEncode(text)){
                return EncodeS(m_Encoding,m_pCharset,m_Split,text);
            }
            else{
                return text;
            }
        }

        #endregion

        #region method Decode

        /// <summary>
        /// Decodes specified encoded-word.
        /// </summary>
        /// <param name="text">Encoded-word value.</param>
        /// <returns>Returns decoded text.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>text</b> is null reference.</exception>
        public string Decode(string text)
        {
            if(text == null){
                throw new ArgumentNullException("text");
            }

            return DecodeS(text);
        }

        #endregion


        #region static method MustEncode

        /// <summary>
        /// Checks if specified text must be encoded.
        /// </summary>
        /// <param name="text">Text to encode.</param>
        /// <returns>Returns true if specified text must be encoded, otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>text</b> is null reference.</exception>
        public static bool MustEncode(string text)
        {
            if(text == null){
                throw new ArgumentNullException("text");
            }

            // Encoding is needed only for non-ASCII chars.

            foreach(char c in text){
                if(c > 127){
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region static method EncodeS

        /// <summary>
        /// Encodes specified text if it contains 8-bit chars, otherwise text won't be encoded.
        /// </summary>
        /// <param name="encoding">Encoding to use to encode text.</param>
        /// <param name="charset">Charset to use for encoding. If not sure UTF-8 is strongly recommended.</param>
        /// <param name="split">If true, words are splitted after 75 chars.</param>
        /// <param name="text">Text to encode.</param>
        /// <returns>Returns encoded text.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>charset</b> or <b>text</b> is null reference.</exception>
        public static string EncodeS(MIME_EncodedWordEncoding encoding,Encoding charset,bool split,string text)
        {
            if(charset == null){
                throw new ArgumentNullException("charset");
            }
            if(text == null){
                throw new ArgumentNullException("text");
            }

            /* RFC 2047 2.
                encoded-word = "=?" charset "?" encoding "?" encoded-text "?="
             
                An 'encoded-word' may not be more than 75 characters long, including
                'charset', 'encoding', 'encoded-text', and delimiters.  If it is
                desirable to encode more text than will fit in an 'encoded-word' of
                75 characters, multiple 'encoded-word's (separated by CRLF SPACE) may
                be used.
             
               RFC 2231 (updates syntax)
                encoded-word := "=?" charset ["*" language] "?" encoded-text "?="
            */

            if(MustEncode(text)){
                List<string> parts = new List<string>();
                if(split){
                    int index = 0;
                    // We just split text to 30 char words, then if some chars encoded, we don't exceed 75 chars lenght limit.
                    while(index < text.Length){
                        int countReaded = Math.Min(30,text.Length - index);
                        parts.Add(text.Substring(index,countReaded));                        
                        index += countReaded;                        
                    }
                }
                else{
                    parts.Add(text);
                }

                StringBuilder retVal = new StringBuilder();
                for(int i=0;i<parts.Count;i++){
                    string part = parts[i];
                    byte[] data = charset.GetBytes(part);

                    #region B encode

                    if(encoding == MIME_EncodedWordEncoding.B){
                        retVal.Append("=?" + charset.WebName + "?B?" + Convert.ToBase64String(data) + "?=");
                    }

                    #endregion

                    #region Q encode

                    else{
                        retVal.Append("=?" + charset.WebName + "?Q?");
                        int stored = 0;
                        foreach(byte b in data){
                            string val = null;
                            // We need to encode byte. Defined in RFC 2047 4.2.
                            if(b > 127 || b == '=' || b == '?' || b == '_' || b == ' '){
                                val = "=" + b.ToString("X2");
                            }
                            else{
                                val = ((char)b).ToString();
                            }

                            retVal.Append(val);
                            stored += val.Length;
                        }
                        retVal.Append("?=");
                    }

                    #endregion

                    if(i < (parts.Count - 1)){
                        retVal.Append("\r\n ");
                    }
                }

                return retVal.ToString();
            }
            else{
                return text;
            }
        }

        #endregion

        #region static method DecodeS

        /// <summary>
        /// Decodes non-ascii word with MIME <b>encoded-word</b> method. Defined in RFC 2047 2.
        /// </summary>
        /// <param name="word">MIME encoded-word value.</param>
        /// <returns>Returns decoded word.</returns>
        /// <remarks>If <b>word</b> is not encoded-word or has invalid syntax, <b>word</b> is leaved as is.</remarks>
        /// <exception cref="ArgumentNullException">Is raised when <b>word</b> is null reference.</exception>
        public static string DecodeS(string word)
        {
            if(word == null){
                throw new ArgumentNullException("word");
            }

            /* RFC 2047 2.
                encoded-word = "=?" charset "?" encoding "?" encoded-text "?="
             
               RFC 2231.
                encoded-word := "=?" charset ["*" language] "?" encoded-text "?="
            */

            try{
                string[] parts = word.Split('?');
                // Not encoded-word.
                if(parts.Length != 5){
                    return word;
                }
                else if(parts[2].ToUpper() == "Q"){
                    return MIME_Utils.QDecode(Encoding.GetEncoding(parts[1].Split('*')[0]),parts[3]);
                }
                else if(parts[2].ToUpper() == "B"){                        
                    return Encoding.GetEncoding(parts[1].Split('*')[0]).GetString(Net_Utils.FromBase64(Encoding.Default.GetBytes(parts[3])));
                }
                // Unknown encoding.
                else{
                    return word;
                }
            }
            catch{
                // Failed to parse encoded-word, leave it as is. RFC 2047 6.3.
                return word;
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets or sets if long words(over 75 char) are splitted.
        /// </summary>
        public bool Split
        {
            get{ return m_Split; }

            set{ m_Split = value; }
        }

        #endregion

    }
}
