namespace Agebull.ZeroNet.Core
{
    /// <summary>
    /// ֡����
    /// </summary>
    public class ZeroFrameType
    {
        /// <summary>
        /// ��ֹ����
        /// </summary>
        public const byte End = (byte) 'E';
        /// <summary>
        /// ִ�мƻ�
        /// </summary>
        public const byte Plan = (byte)'P';
        /// <summary>
        /// ����
        /// </summary>
        public const byte Argument = (byte)'A';
        /// <summary>
        /// ����ID
        /// </summary>
        public const byte RequestId = (byte)'I';
        /// <summary>
        /// ������
        /// </summary>
        public const byte Requester = (byte)'R';
        /// <summary>
        /// ������/������
        /// </summary>
        public const byte Publisher = Requester;
        /// <summary>
        /// �ظ���
        /// </summary>
        public const byte Responser = (byte)'G';
        /// <summary>
        /// ������/�˷���
        /// </summary>
        public const byte Subscriber = (byte)Responser;
        //�㲥����
        //#define zero_pub_title  '*'
        /// <summary>
        /// �㲥����
        /// </summary>
        public const byte SubTitle = (byte)'S';
        /// <summary>
        /// ������������Ϣ
        /// </summary>
        public const byte Context = (byte)'T';
    }
}