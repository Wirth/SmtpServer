﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SmtpServer.Mail;
using SmtpServer.Protocol.Text;

namespace SmtpServer.Protocol
{
    /// <remarks>
    /// This class is responsible for parsing the SMTP command arguments according to the ANBF described in
    /// the RFC http://tools.ietf.org/html/rfc5321#section-4.1.2
    /// </remarks>
    public sealed class SmtpParser
    {
        /// <summary>
        /// Try to make a reverse path.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the reverse path from.</param>
        /// <param name="mailbox">The reverse path that was made, or undefined if it was not made.</param>
        /// <returns>true if the reverse path was made, false if not.</returns>
        /// <remarks><![CDATA[Path / "<>"]]></remarks>
        public bool TryMakeReversePath(TokenEnumerator enumerator, out IMailbox mailbox)
        {
            if (TryMake(enumerator, TryMakePath, out mailbox))
            {
                return true;
            }

            if (enumerator.Take() != new Token(TokenKind.Symbol, "<"))
            {
                return false;
            }

            // not valid according to the spec but some senders do it
            enumerator.TakeWhile(t => t.Kind == TokenKind.Space);

            if (enumerator.Take() != new Token(TokenKind.Symbol, ">"))
            {
                return false;
            }

            mailbox = null;

            return true;
        }

        /// <summary>
        /// Try to make a path.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the path from.</param>
        /// <param name="mailbox">The path that was made, or undefined if it was not made.</param>
        /// <returns>true if the path was made, false if not.</returns>
        /// <remarks><![CDATA["<" [ A-d-l ":" ] Mailbox ">"]]></remarks>
        public bool TryMakePath(TokenEnumerator enumerator, out IMailbox mailbox)
        {
            mailbox = null;
            var haveHook = enumerator.Take() == new Token(TokenKind.Symbol, "<");
            if (!haveHook) enumerator.Take(-1);

            // Note, the at-domain-list must be matched, but also must be ignored
            // http://tools.ietf.org/html/rfc5321#appendix-C
            string atDomainList;
            if (TryMake(enumerator, TryMakeAtDomainList, out atDomainList))
            {
                // if the @domain list was matched then it needs to be followed by a colon
                if (enumerator.Take() != new Token(TokenKind.Punctuation, ":"))
                {
                    return false;
                }
            }

            if (TryMake(enumerator, TryMakeMailbox, out mailbox) == false)
            {
                return false;
            }

            if (haveHook) return enumerator.Take() == new Token(TokenKind.Symbol, ">");

            return true;
        }

        /// <summary>
        /// Try to make an @domain list.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the @domain list from.</param>
        /// <param name="atDomainList">The @domain list that was made, or undefined if it was not made.</param>
        /// <returns>true if the @domain list was made, false if not.</returns>
        /// <remarks><![CDATA[At-domain *( "," At-domain )]]></remarks>
        public bool TryMakeAtDomainList(TokenEnumerator enumerator, out string atDomainList)
        {
            if (TryMake(enumerator, TryMakeAtDomain, out atDomainList) == false)
            {
                return false;
            }

            // match the optional list
            while (enumerator.Peek() == new Token(TokenKind.Punctuation, ","))
            {
                enumerator.Take();

                string atDomain;
                if (TryMake(enumerator, TryMakeAtDomain, out atDomain) == false)
                {
                    return false;
                }

                atDomainList += String.Format(",{0}", atDomain);
            }

            return true;
        }

        /// <summary>
        /// Try to make an @domain.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the @domain from.</param>
        /// <param name="atDomain">The @domain that was made, or undefined if it was not made.</param>
        /// <returns>true if the @domain was made, false if not.</returns>
        /// <remarks><![CDATA["@" Domain]]></remarks>
        public bool TryMakeAtDomain(TokenEnumerator enumerator, out string atDomain)
        {
            atDomain = null;

            if (enumerator.Take() != new Token(TokenKind.Punctuation, "@"))
            {
                return false;
            }

            string domain;
            if (TryMake(enumerator, TryMakeDomain, out domain) == false)
            {
                return false;
            }

            atDomain = String.Format("@{0}", domain);

            return true;
        }

        /// <summary>
        /// Try to make a mailbox.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the mailbox from.</param>
        /// <param name="mailbox">The mailbox that was made, or undefined if it was not made.</param>
        /// <returns>true if the mailbox was made, false if not.</returns>
        /// <remarks><![CDATA[Local-part "@" ( Domain / address-literal )]]></remarks>
        public bool TryMakeMailbox(TokenEnumerator enumerator, out IMailbox mailbox)
        {
            mailbox = null;

            string localpart;
            if (TryMake(enumerator, TryMakeLocalPart, out localpart) == false)
            {
                return false;
            }

            if (enumerator.Take() != new Token(TokenKind.Punctuation, "@"))
            {
                return false;
            }

            string domain;
            if (TryMake(enumerator, TryMakeDomain, out domain))
            {
                mailbox = new Mailbox(localpart, domain);

                return true;
            }

            string address;
            if (TryMake(enumerator, TryMakeAddressLiteral, out address))
            {
                mailbox = new Mailbox(localpart, address);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Try to make a domain name.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the domain name from.</param>
        /// <param name="domain">The domain name that was made, or undefined if it was not made.</param>
        /// <returns>true if the domain name was made, false if not.</returns>
        /// <remarks><![CDATA[sub-domain *("." sub-domain)]]></remarks>
        public bool TryMakeDomain(TokenEnumerator enumerator, out string domain)
        {
            if (TryMake(enumerator, TryMakeSubdomain, out domain) == false)
            {
                return false;
            }

            while (enumerator.Peek() == new Token(TokenKind.Punctuation, "."))
            {
                enumerator.Take();

                string subdomain;
                if (TryMake(enumerator, TryMakeSubdomain, out subdomain) == false)
                {
                    return false;
                }

                domain += String.Concat(".", subdomain);
            }

            return true;
        }

        /// <summary>
        /// Try to make a subdomain name.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the domain name from.</param>
        /// <param name="subdomain">The subdomain name that was made, or undefined if it was not made.</param>
        /// <returns>true if the subdomain name was made, false if not.</returns>
        /// <remarks><![CDATA[Let-dig [Ldh-str]]]></remarks>
        public bool TryMakeSubdomain(TokenEnumerator enumerator, out string subdomain)
        {
            if (TryMake(enumerator, TryMakeTextOrNumber, out subdomain) == false)
            {
                return false;
            }

            string letterNumberHyphen;
            if (TryMake(enumerator, TryMakeTextOrNumberOrHyphenString, out letterNumberHyphen) == false)
            {
                return subdomain != null;
            }

            subdomain += letterNumberHyphen;

            return true;
        }

        /// <summary>
        /// Try to make a address.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the address from.</param>
        /// <param name="address">The address that was made, or undefined if it was not made.</param>
        /// <returns>true if the address was made, false if not.</returns>
        /// <remarks><![CDATA["[" ( IPv4-address-literal / IPv6-address-literal / General-address-literal ) "]"]]></remarks>
        public bool TryMakeAddressLiteral(TokenEnumerator enumerator, out string address)
        {
            address = null;

            if (enumerator.Take() != new Token(TokenKind.Punctuation, "["))
            {
                return false;
            }

            // skip any whitespace
            enumerator.TakeWhile(t => t.Kind == TokenKind.Space);

            if (TryMake(enumerator, TryMakeIpv4AddressLiteral, out address) == false)
            {
                return false;
            }

            // skip any whitespace
            enumerator.TakeWhile(t => t.Kind == TokenKind.Space);

            if (enumerator.Take() != new Token(TokenKind.Punctuation, "]"))
            {
                return false;
            }

            return address != null;
        }

        /// <summary>
        /// Try to make an IPv4 address literal.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the address from.</param>
        /// <param name="address">The address that was made, or undefined if it was not made.</param>
        /// <returns>true if the address was made, false if not.</returns>
        /// <remarks><![CDATA[ Snum 3("."  Snum) ]]></remarks>
        public bool TryMakeIpv4AddressLiteral(TokenEnumerator enumerator, out string address)
        {
            address = null;

            int snum;
            if (TryMake(enumerator, TryMakeSnum, out snum) == false)
            {
                return false;
            }

            address = snum.ToString(CultureInfo.InvariantCulture);

            for (var i = 0; i < 3 && enumerator.Peek() == new Token(TokenKind.Punctuation, "."); i++)
            {
                enumerator.Take();

                if (TryMake(enumerator, TryMakeSnum, out snum) == false)
                {
                    return false;
                }

                address = String.Concat(address, '.', snum);
            }

            return true;
        }

        /// <summary>
        /// Try to make an Snum (number in the range of 0-255).
        /// </summary>
        /// <param name="enumerator">The enumerator to make the address from.</param>
        /// <param name="snum">The snum that was made, or undefined if it was not made.</param>
        /// <returns>true if the snum was made, false if not.</returns>
        /// <remarks><![CDATA[ 1*3DIGIT ]]></remarks>
        public bool TryMakeSnum(TokenEnumerator enumerator, out int snum)
        {
            var token = enumerator.Take();

            if (Int32.TryParse(token.Text, out snum) && token.Kind == TokenKind.Number)
            {
                return snum >= 0 && snum <= 255;
            }

            return false;
        }

        /// <summary>
        /// Try to make a text/number/hyphen string.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the text/number/hyphen from.</param>
        /// <param name="textOrNumberOrHyphenString">The text, number, or hyphen that was matched, or undefined if it was not matched.</param>
        /// <returns>true if a text, number or hyphen was made, false if not.</returns>
        /// <remarks><![CDATA[*( ALPHA / DIGIT / "-" ) Let-dig]]></remarks>
        public bool TryMakeTextOrNumberOrHyphenString(TokenEnumerator enumerator, out string textOrNumberOrHyphenString)
        {
            textOrNumberOrHyphenString = null;

            var token = enumerator.Peek();
            while (token.Kind == TokenKind.Text || token.Kind == TokenKind.Number || token == new Token(TokenKind.Punctuation, "-"))
            {
                textOrNumberOrHyphenString += enumerator.Take().Text;

                token = enumerator.Peek();
            }

            // can not end with a hyphen
            return textOrNumberOrHyphenString != null && token != new Token(TokenKind.Punctuation, "-");
        }

        /// <summary>
        /// Try to make a text or number
        /// </summary>
        /// <param name="enumerator">The enumerator to make a text or number from.</param>
        /// <param name="textOrNumber">The text or number that was made, or undefined if it was not made.</param>
        /// <returns>true if the text or number was made, false if not.</returns>
        /// <remarks><![CDATA[ALPHA / DIGIT]]></remarks>
        public bool TryMakeTextOrNumber(TokenEnumerator enumerator, out string textOrNumber)
        {
            var token = enumerator.Take();

            textOrNumber = token.Text;

            return token.Kind == TokenKind.Text || token.Kind == TokenKind.Number;
        }

        /// <summary>
        /// Try to make the local part of the path.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the local part from.</param>
        /// <param name="localPart">The local part that was made, or undefined if it was not made.</param>
        /// <returns>true if the local part was made, false if not.</returns>
        /// <remarks><![CDATA[Dot-string / Quoted-string]]></remarks>
        public bool TryMakeLocalPart(TokenEnumerator enumerator, out string localPart)
        {
            if (TryMake(enumerator, TryMakeDotString, out localPart))
            {
                return true;
            }

            // TODO: TryMakeQuotedString(...)

            return false;
        }

        /// <summary>
        /// Try to make a dot-string from the tokens.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the dot-string from.</param>
        /// <param name="dotString">The dot-string that was made, or undefined if it was not made.</param>
        /// <returns>true if the dot-string was made, false if not.</returns>
        /// <remarks><![CDATA[Atom *("."  Atom)]]></remarks>
        public bool TryMakeDotString(TokenEnumerator enumerator, out string dotString)
        {
            if (TryMake(enumerator, TryMakeAtom, out dotString) == false)
            {
                return false;
            }

            while (enumerator.Peek() == new Token(TokenKind.Punctuation, "."))
            {
                // skip the punctuation
                enumerator.Take();

                string atom;
                if (TryMake(enumerator, TryMakeAtom, out atom) == false)
                {
                    return true;
                }

                dotString += String.Concat(".", atom);
            }

            return true;
        }

        /// <summary>
        /// Try to make an "Atom" from the tokens.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the atom from.</param>
        /// <param name="atom">The atom that was made, or undefined if it was not made.</param>
        /// <returns>true if the atom was made, false if not.</returns>
        /// <remarks><![CDATA[1*atext]]></remarks>
        public bool TryMakeAtom(TokenEnumerator enumerator, out string atom)
        {
            atom = null;

            string atext;
            while (TryMake(enumerator, TryMakeAtext, out atext))
            {
                atom += atext;
            }

            return atom != null;
        }

        /// <summary>
        /// Try to make an "Atext" from the tokens.
        /// </summary>
        /// <param name="enumerator">The enumerator to make the atext from.</param>
        /// <param name="atext">The atext that was made, or undefined if it was not made.</param>
        /// <returns>true if the atext was made, false if not.</returns>
        /// <remarks><![CDATA[atext]]></remarks>
        public bool TryMakeAtext(TokenEnumerator enumerator, out string atext)
        {
            atext = null;

            var token = enumerator.Take();
            switch (token.Kind)
            {
                case TokenKind.Text:
                case TokenKind.Number:
                    atext = token.Text;
                    return true;

                case TokenKind.Punctuation:
                    switch (token.Text[0])
                    {
                        case '!':
                        case '#':
                        case '%':
                        case '&':
                        case '\'':
                        case '*':
                        case '-':
                        case '/':
                        case '?':
                        case '_':
                        case '{':
                        case '}':
                            atext = token.Text;
                            return true;
                    }
                    break;

                case TokenKind.Symbol:
                    switch (token.Text[0])
                    {
                        case '$':
                        case '+':
                        case '=':
                        case '^':
                        case '`':
                        case '|':
                        case '~':
                            atext = token.Text;
                            return true;
                    }
                    break;
            }

            return false;
        }

        /// <summary>
        /// Try to make an Mail-Parameters from the tokens.
        /// </summary>
        /// <param name="enumerator">The enumerator to perform the make on.</param>
        /// <param name="parameters">The mail parameters that were made.</param>
        /// <returns>true if the mail parameters can be made, false if not.</returns>
        /// <remarks><![CDATA[esmtp-param *(SP esmtp-param)]]></remarks>
        public bool TryMakeMailParameters(TokenEnumerator enumerator, out IDictionary<string, string> parameters)
        {
            parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (enumerator.Peek().Kind != TokenKind.None)
            {
                KeyValuePair<string, string> parameter;
                if (TryMake(enumerator, TryMakeEsmtpParameter, out parameter) == false)
                {
                    return false;
                }

                parameters.Add(parameter);
                enumerator.TakeWhile(t => t.Kind == TokenKind.Space);
            }

            return parameters.Count > 0;
        }

        /// <summary>
        /// Try to make an Esmtp-Parameter from the tokens.
        /// </summary>
        /// <param name="enumerator">The enumerator to perform the make on.</param>
        /// <param name="parameter">The esmtp-parameter that was made.</param>
        /// <returns>true if the esmtp-parameter can be made, false if not.</returns>
        /// <remarks><![CDATA[esmtp-keyword ["=" esmtp-value]]]></remarks>
        public bool TryMakeEsmtpParameter(TokenEnumerator enumerator, out KeyValuePair<string, string> parameter)
        {
            parameter = default(KeyValuePair<string, string>);

            string keyword;
            if (TryMake(enumerator, TryMakeEsmtpKeyword, out keyword) == false)
            {
                return false;
            }

            if (enumerator.Take() != new Token(TokenKind.Symbol, "="))
            {
                return false;
            }

            string value;
            if (TryMake(enumerator, TryMakeEsmtpValue, out value) == false)
            {
                return false;
            }

            parameter = new KeyValuePair<string, string>(keyword, value);
            
            return true;
        }

        /// <summary>
        /// Try to make an Esmtp-Keyword from the tokens.
        /// </summary>
        /// <param name="enumerator">The enumerator to perform the make on.</param>
        /// <param name="keyword">The esmtp-keyword that was made.</param>
        /// <returns>true if the esmtp-keyword can be made, false if not.</returns>
        /// <remarks><![CDATA[(ALPHA / DIGIT) *(ALPHA / DIGIT / "-")]]></remarks>
        public bool TryMakeEsmtpKeyword(TokenEnumerator enumerator, out string keyword)
        {
            keyword = null;

            var token = enumerator.Peek();
            while (token.Kind == TokenKind.Text || token.Kind == TokenKind.Number || token == new Token(TokenKind.Punctuation, "-"))
            {
                keyword += enumerator.Take().Text;

                token = enumerator.Peek();
            }

            return keyword != null;
        }

        /// <summary>
        /// Try to make an Esmtp-Value from the tokens.
        /// </summary>
        /// <param name="enumerator">The enumerator to perform the make on.</param>
        /// <param name="value">The esmtp-value that was made.</param>
        /// <returns>true if the esmtp-value can be made, false if not.</returns>
        /// <remarks><![CDATA[1*(%d33-60 / %d62-127)]]></remarks>
        public bool TryMakeEsmtpValue(TokenEnumerator enumerator, out string value)
        {
            value = null;

            var token = enumerator.Peek();
            while (token.Text.Length > 0 && token.Text.ToCharArray().All(ch => (ch >= 33 && ch <= 66) || (ch >= 62 && ch <= 127)))
            {
                value += enumerator.Take().Text;

                token = enumerator.Peek();
            }

            return value != null;
        }

        /// <summary>
        /// Try to make a base64 encoded string.
        /// </summary>
        /// <param name="enumerator">The enumerator to perform the make on.</param>
        /// <param name="base64">The base64 encoded string that were found.</param>
        /// <returns>true if the base64 encoded string can be made, false if not.</returns>
        /// <remarks><![CDATA[ALPHA / DIGIT / "+" / "/"]]></remarks>
        public bool TryMakeBase64(TokenEnumerator enumerator, out string base64)
        {
            base64 = null;

            while (enumerator.Peek().Kind != TokenKind.None)
            {
                string base64Chars;
                if (TryMake(enumerator, TryMakeBase64Chars, out base64Chars) == false)
                {
                    return false;
                }

                base64 += base64Chars;
            }

            // because the TryMakeBase64Chars method matches tokens, each Text token could make
            // up several Base64 encoded "bytes" so we ensure that we have a length divisible by 4
            return base64 != null && base64.Length % 4 == 0;
        }

        /// <summary>
        /// Try to make the allowable characters in a base64 encoded string.
        /// </summary>
        /// <param name="enumerator">The enumerator to perform the make on.</param>
        /// <param name="base64Chars">The base64 characters that were found.</param>
        /// <returns>true if the base64-chars can be made, false if not.</returns>
        /// <remarks><![CDATA[ALPHA / DIGIT / "+" / "/"]]></remarks>
        static bool TryMakeBase64Chars(TokenEnumerator enumerator, out string base64Chars)
        {
            base64Chars = null;

            var token = enumerator.Take();
            switch (token.Kind)
            {
                case TokenKind.Text:
                case TokenKind.Number:
                    base64Chars = token.Text;
                    return true;

                case TokenKind.Punctuation:
                    switch (token.Text[0])
                    {
                        case '/':
                            base64Chars = token.Text;
                            return true;
                    }
                    break;

                case TokenKind.Symbol:
                    switch (token.Text[0])
                    {
                        case '+':
                            base64Chars = token.Text;
                            return true;
                    }
                    break;
            }

            return false;
        }
        
        /// <summary>
        /// Delegate for the TryMake function to allow for "out" parameters.
        /// </summary>
        /// <typeparam name="T">The type of the out parameter.</typeparam>
        /// <param name="enumerator">The enumerator to perform the make on.</param>
        /// <param name="found">The out parameter that was found during the make operation.</param>
        /// <returns>true if the make operation found a parameter, false if not.</returns>
        delegate bool TryMakeDelegate<T>(TokenEnumerator enumerator, out T found);

        /// <summary>
        /// Try to match a function on the enumerator ensuring that the enumerator is restored on failure.
        /// </summary>
        /// <param name="enumerator">The enumerator to attempt to match on.</param>
        /// <param name="make">The callback function to match on the enumerator.</param>
        /// <param name="found">The out value that was found.</param>
        /// <returns>true if the matching function successfully made a match, false if not.</returns>
        static bool TryMake<T>(TokenEnumerator enumerator, TryMakeDelegate<T> make, out T found)
        {
            using (var checkpoint = enumerator.Checkpoint())
            {
                if (make(enumerator, out found))
                {
                    return true;
                }

                checkpoint.Rollback();
            }

            return false;
        }
    }
}
