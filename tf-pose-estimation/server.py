import logging

import cv2
import json
import numpy as np
import common

from tf_pose.estimator import TfPoseEstimator
from tf_pose.networks import get_graph_path, model_wh
from websocket_server import WebsocketServer
from lifting.prob_model import Prob3dPose

PORT = 5000
HOST = '127.0.0.1'

# logger_setup
logger = logging.getLogger(__name__)
logger.setLevel(logging.INFO)
handler = logging.StreamHandler()
handler.setFormatter(logging.Formatter(' %(module)s -  %(asctime)s - %(levelname)s - %(message)s'))
logger.addHandler(handler)


def create_json(pose3d):
    global old_data

    data = {'body_parts': []}

    """
    // 0 :Hip
    // 1 :RHip
    // 2 :RKnee
    // 3 :RFoot
    // 4 :LHip
    // 5 :LKnee
    // 6 :LFoot
    // 7 :Spine
    // 8 :Thorax
    // 9 :Neck/Nose
    // 10:Head
    // 11:LShoulder
    // 12:LElbow
    // 13:LWrist
    // 14:RShoulder
    // 15:RElbow
    // 16:RWrist
    """

    for i in range(17):
        data['body_parts'].append({'id': i, 'x': pose3d[0][0][i], 'y': pose3d[0][2][i], 'z': pose3d[0][1][i]})

    old_data = data
    return data


def new_client(client, server):
    logger.info('NewClient {}:{} has left.'.format(client['address'][0], client['address'][1]))


def client_left(client, server):
    logger.info('Client {}:{} has left.'.format(client['address'][0], client['address'][1]))


def message_received(client, server, message):
    _, image = cam.read()

    humans = e.inference(image, resize_to_default=(w > 0 and h > 0), upsample_size=4.0)

    pose_2d_mpiis = []
    visibilities = []

    standard_w = 640
    standard_h = 480

    try:
        pose_2d_mpii, visibility = common.MPIIPart.from_coco(humans[0])
        pose_2d_mpiis.append([(int(x * standard_w + 0.5), int(y * standard_h + 0.5)) for x, y in pose_2d_mpii])
        visibilities.append(visibility)
        pose_2d_mpiis = np.array(pose_2d_mpiis)
        visibilities = np.array(visibilities)
        transformed_pose2d, weights = poseLifting.transform_joints(pose_2d_mpiis, visibilities)
        pose_3d = poseLifting.compute_3d(transformed_pose2d, weights)
        print(pose_3d)
        server.send_message(client, json.dumps(create_json(pose_3d)))

    except :
        server.send_message(client, json.dumps(old_data))


if __name__ == '__main__':
    # main
    w, h = model_wh("432x368")
    e = TfPoseEstimator(get_graph_path("mobilenet_thin"), target_size=(432, 368), trt_bool=False)
    poseLifting = Prob3dPose('lifting/models/prob_model_params.mat')

    cam = cv2.VideoCapture(0)

    old_data = {}

    server = WebsocketServer(port=PORT, host=HOST)
    server.set_fn_new_client(new_client)
    server.set_fn_client_left(client_left)
    server.set_fn_message_received(message_received)
    server.run_forever()
